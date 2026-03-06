using System;
using System.Collections.Generic;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class DiffEngine
{
    public static List<DiffItem> ComputeDiffs(
        IReadOnlyList<ChapterPair> pairs,
        double similarityThreshold = 0.85d,
        CancellationToken cancellationToken = default)
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
                cancellationToken.ThrowIfCancellationRequested();

                if (pair.Source is null && pair.Target is null)
                {
                    continue;
                }

                if (pair.Source is null && pair.Target is not null)
                {
                    result.Add(new DiffItem(
                        pair.Target.Key,
                        ChangeType.Added,
                        string.Empty,
                        GetChapterText(pair.Target),
                        0d,
                        BuildPageRef(null, pair.Target)));

                    continue;
                }

                if (pair.Source is not null && pair.Target is null)
                {
                    result.Add(new DiffItem(
                        pair.Source.Key,
                        ChangeType.Deleted,
                        GetChapterText(pair.Source),
                        string.Empty,
                        0d,
                        BuildPageRef(pair.Source, null)));

                    continue;
                }

                AppendChapterDiffs(result, pair.Source!, pair.Target!, similarityThreshold, cancellationToken);
            }

            return result;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }

    public static List<DiffItem> ComputeDiffs(
        ChapterMatchResult matchResult,
        double similarityThreshold = 0.85d,
        CancellationToken cancellationToken = default)
    {
        if (matchResult is null)
        {
            throw new ArgumentNullException(nameof(matchResult));
        }

        return ComputeDiffs(matchResult.AllPairs, similarityThreshold, cancellationToken);
    }

    private static void AppendChapterDiffs(
        List<DiffItem> destination,
        ChapterNode source,
        ChapterNode target,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        AppendTitleDiffIfNeeded(destination, source, target);

        var sourceBlocks = source.Blocks;
        var targetBlocks = target.Blocks;
        if (sourceBlocks.Count == 0 && targetBlocks.Count == 0)
        {
            return;
        }

        var pageRef = BuildPageRef(source, target);
        var alignments = AlignBlocks(sourceBlocks, targetBlocks, cancellationToken);

        foreach (var alignment in alignments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (alignment.SourceIndex >= 0 && alignment.TargetIndex >= 0)
            {
                var before = sourceBlocks[alignment.SourceIndex];
                var after = targetBlocks[alignment.TargetIndex];
                var similarity = alignment.Similarity;

                if (before.NormalizedText.Equals(after.NormalizedText, StringComparison.Ordinal))
                {
                    continue;
                }

                if (similarity >= similarityThreshold)
                {
                    destination.Add(new DiffItem(
                        source.Key,
                        ChangeType.Modified,
                        before.OriginalText,
                        after.OriginalText,
                        Math.Clamp(similarity, 0d, 1d),
                        pageRef));
                }
                else
                {
                    destination.Add(new DiffItem(
                        source.Key,
                        ChangeType.Deleted,
                        before.OriginalText,
                        string.Empty,
                        Math.Clamp(similarity, 0d, 1d),
                        pageRef));

                    destination.Add(new DiffItem(
                        target.Key,
                        ChangeType.Added,
                        string.Empty,
                        after.OriginalText,
                        Math.Clamp(similarity, 0d, 1d),
                        pageRef));
                }

                continue;
            }

            if (alignment.SourceIndex >= 0)
            {
                destination.Add(new DiffItem(
                    source.Key,
                    ChangeType.Deleted,
                    sourceBlocks[alignment.SourceIndex].OriginalText,
                    string.Empty,
                    0d,
                    pageRef));

                continue;
            }

            if (alignment.TargetIndex >= 0)
            {
                destination.Add(new DiffItem(
                    target.Key,
                    ChangeType.Added,
                    string.Empty,
                    targetBlocks[alignment.TargetIndex].OriginalText,
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

        var similarity = SimilarityCalculator.CalculateHybrid(source.Title, target.Title, ignoreCase: true);
        destination.Add(new DiffItem(
            source.Key,
            ChangeType.Modified,
            source.Title,
            target.Title,
            Math.Clamp(similarity, 0d, 1d),
            BuildPageRef(source, target)));
    }

    private static List<BlockAlignment> AlignBlocks(
        IReadOnlyList<TextBlock> source,
        IReadOnlyList<TextBlock> target,
        CancellationToken cancellationToken)
    {
        if (source.Count == 0 && target.Count == 0)
        {
            return new List<BlockAlignment>();
        }

        var n = source.Count;
        var m = target.Count;
        const double gapPenalty = 0.45d;

        var score = new double[n + 1, m + 1];
        var trace = new Step[n + 1, m + 1];
        var similarity = new double[n, m];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < m; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                similarity[i, j] = SimilarityCalculator.CalculateHybrid(
                    source[i].NormalizedText,
                    target[j].NormalizedText,
                    ignoreCase: true);
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

        var alignments = new List<BlockAlignment>(n + m);
        var row = n;
        var col = m;
        while (row > 0 || col > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = trace[row, col];

            if (step == Step.Match && row > 0 && col > 0)
            {
                alignments.Add(new BlockAlignment(row - 1, col - 1, similarity[row - 1, col - 1]));
                row--;
                col--;
                continue;
            }

            if (step == Step.Delete && row > 0)
            {
                alignments.Add(new BlockAlignment(row - 1, -1, 0d));
                row--;
                continue;
            }

            if (col > 0)
            {
                alignments.Add(new BlockAlignment(-1, col - 1, 0d));
                col--;
            }
            else
            {
                alignments.Add(new BlockAlignment(row - 1, -1, 0d));
                row--;
            }
        }

        alignments.Reverse();
        return alignments;
    }

    private static string GetChapterText(ChapterNode chapter)
    {
        return chapter.Content;
    }

    private static string BuildPageRef(ChapterNode? source, ChapterNode? target)
    {
        var start = source?.PageStart ?? target?.PageStart ?? 0;
        var end = source?.PageEnd ?? target?.PageEnd ?? start;
        return PageReferenceFormatter.Format(start, end);
    }

    private enum Step
    {
        None,
        Match,
        Delete,
        Insert
    }

    private readonly record struct BlockAlignment(int SourceIndex, int TargetIndex, double Similarity);
}
