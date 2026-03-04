using System;
using System.Collections.Generic;
using System.Linq;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class DiffEngine
{
    public static List<DiffItem> ComputeDiffs(IReadOnlyList<ChapterPair> pairs, double similarityThreshold = 0.85d)
    {
        if (pairs is null)
        {
            throw new ArgumentNullException(nameof(pairs));
        }

        if (similarityThreshold <= 0d || similarityThreshold > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(similarityThreshold));
        }

        try
        {
            var result = new List<DiffItem>();

            foreach (var pair in pairs)
            {
                if (pair.Source is null && pair.Target is null)
                {
                    continue;
                }

                if (pair.Source is null && pair.Target is not null)
                {
                    result.Add(CreateItem(
                        pair.Target.Key,
                        ChangeType.Added,
                        string.Empty,
                        pair.Target.Content.ToString(),
                        0d,
                        BuildPageRef(null, pair.Target)));

                    continue;
                }

                if (pair.Source is not null && pair.Target is null)
                {
                    result.Add(CreateItem(
                        pair.Source.Key,
                        ChangeType.Deleted,
                        pair.Source.Content.ToString(),
                        string.Empty,
                        0d,
                        BuildPageRef(pair.Source, null)));

                    continue;
                }

                AppendChapterDiffs(result, pair.Source!, pair.Target!, similarityThreshold);
            }

            return result;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }

    public static List<DiffItem> ComputeDiffs(ChapterMatchResult matchResult, double similarityThreshold = 0.85d)
    {
        if (matchResult is null)
        {
            throw new ArgumentNullException(nameof(matchResult));
        }

        var pairs = new List<ChapterPair>(matchResult.Matches);
        pairs.AddRange(matchResult.UnmatchedSource.Select(node => new ChapterPair(node, null, 0d)));
        pairs.AddRange(matchResult.UnmatchedTarget.Select(node => new ChapterPair(null, node, 0d)));

        return ComputeDiffs(pairs, similarityThreshold);
    }

    private static void AppendChapterDiffs(
        List<DiffItem> destination,
        ChapterNode source,
        ChapterNode target,
        double similarityThreshold)
    {
        AppendTitleDiffIfNeeded(destination, source, target);

        var sourceParagraphs = SplitParagraphs(source.Content.ToString());
        var targetParagraphs = SplitParagraphs(target.Content.ToString());
        if (sourceParagraphs.Length == 0 && targetParagraphs.Length == 0)
        {
            return;
        }

        var pageRef = BuildPageRef(source, target);
        var alignments = AlignParagraphs(sourceParagraphs, targetParagraphs);

        foreach (var alignment in alignments)
        {
            if (alignment.SourceIndex >= 0 && alignment.TargetIndex >= 0)
            {
                var before = sourceParagraphs[alignment.SourceIndex];
                var after = targetParagraphs[alignment.TargetIndex];
                var similarity = alignment.Similarity;

                if (before.Equals(after, StringComparison.Ordinal))
                {
                    continue;
                }

                if (similarity >= similarityThreshold)
                {
                    destination.Add(CreateItem(
                        source.Key,
                        ChangeType.Modified,
                        before,
                        after,
                        similarity,
                        pageRef));
                }
                else
                {
                    destination.Add(CreateItem(
                        source.Key,
                        ChangeType.Deleted,
                        before,
                        string.Empty,
                        similarity,
                        pageRef));

                    destination.Add(CreateItem(
                        target.Key,
                        ChangeType.Added,
                        string.Empty,
                        after,
                        similarity,
                        pageRef));
                }

                continue;
            }

            if (alignment.SourceIndex >= 0)
            {
                destination.Add(CreateItem(
                    source.Key,
                    ChangeType.Deleted,
                    sourceParagraphs[alignment.SourceIndex],
                    string.Empty,
                    0d,
                    pageRef));

                continue;
            }

            if (alignment.TargetIndex >= 0)
            {
                destination.Add(CreateItem(
                    target.Key,
                    ChangeType.Added,
                    string.Empty,
                    targetParagraphs[alignment.TargetIndex],
                    0d,
                    pageRef));
            }
        }
    }

    private static void AppendTitleDiffIfNeeded(List<DiffItem> destination, ChapterNode source, ChapterNode target)
    {
        if (source.Title.Equals(target.Title, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var similarity = SimilarityCalculator.Calculate(source.Title, target.Title, ignoreCase: true);
        destination.Add(CreateItem(
            source.Key,
            ChangeType.Modified,
            source.Title,
            target.Title,
            similarity,
            BuildPageRef(source, target)));
    }

    private static List<ParagraphAlignment> AlignParagraphs(IReadOnlyList<string> source, IReadOnlyList<string> target)
    {
        if (source.Count == 0 && target.Count == 0)
        {
            return new List<ParagraphAlignment>();
        }

        var n = source.Count;
        var m = target.Count;
        const double gapPenalty = 0.35d;

        var score = new double[n + 1, m + 1];
        var trace = new Step[n + 1, m + 1];
        var similarity = new double[n, m];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < m; j++)
            {
                similarity[i, j] = SimilarityCalculator.Calculate(source[i], target[j], ignoreCase: true);
            }
        }

        for (var i = 1; i <= n; i++)
        {
            score[i, 0] = score[i - 1, 0] - gapPenalty;
            trace[i, 0] = Step.Delete;
        }

        for (var j = 1; j <= m; j++)
        {
            score[0, j] = score[0, j - 1] - gapPenalty;
            trace[0, j] = Step.Insert;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var matchScore = score[i - 1, j - 1] + similarity[i - 1, j - 1];
                var deleteScore = score[i - 1, j] - gapPenalty;
                var insertScore = score[i, j - 1] - gapPenalty;

                if (matchScore >= deleteScore && matchScore >= insertScore)
                {
                    score[i, j] = matchScore;
                    trace[i, j] = Step.Match;
                }
                else if (deleteScore >= insertScore)
                {
                    score[i, j] = deleteScore;
                    trace[i, j] = Step.Delete;
                }
                else
                {
                    score[i, j] = insertScore;
                    trace[i, j] = Step.Insert;
                }
            }
        }

        var alignments = new List<ParagraphAlignment>(n + m);
        var row = n;
        var col = m;
        while (row > 0 || col > 0)
        {
            var step = trace[row, col];

            if (step == Step.Match && row > 0 && col > 0)
            {
                alignments.Add(new ParagraphAlignment(row - 1, col - 1, similarity[row - 1, col - 1]));
                row--;
                col--;
                continue;
            }

            if (step == Step.Delete && row > 0)
            {
                alignments.Add(new ParagraphAlignment(row - 1, -1, 0d));
                row--;
                continue;
            }

            if (col > 0)
            {
                alignments.Add(new ParagraphAlignment(-1, col - 1, 0d));
                col--;
            }
            else
            {
                alignments.Add(new ParagraphAlignment(row - 1, -1, 0d));
                row--;
            }
        }

        alignments.Reverse();
        return alignments;
    }

    private static DiffItem CreateItem(
        string chapterKey,
        ChangeType changeType,
        string before,
        string after,
        double similarity,
        string pageRef)
    {
        return new DiffItem
        {
            ChapterKey = chapterKey ?? string.Empty,
            ChangeType = changeType,
            BeforeText = before ?? string.Empty,
            AfterText = after ?? string.Empty,
            SimilarityScore = Math.Clamp(similarity, 0d, 1d),
            PageRef = pageRef ?? string.Empty
        };
    }

    private static string BuildPageRef(ChapterNode? source, ChapterNode? target)
    {
        var start = source?.PageStart ?? target?.PageStart ?? 0;
        var end = source?.PageEnd ?? target?.PageEnd ?? start;
        return PageReferenceFormatter.Format(start, end);
    }

    private static string[] SplitParagraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();
    }

    private enum Step
    {
        None,
        Match,
        Delete,
        Insert
    }

    private readonly record struct ParagraphAlignment(int SourceIndex, int TargetIndex, double Similarity);
}
