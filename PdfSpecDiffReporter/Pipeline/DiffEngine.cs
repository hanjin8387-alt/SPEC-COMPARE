using System;
using System.Collections.Generic;
using System.Linq;
using DiffPlex;
using DiffPlex.Chunkers;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class DiffEngine
{
    private static readonly IDiffer Differ = new Differ();

    /// <summary>
    /// Computes chapter-level diffs from matched source/target chapter pairs.
    /// </summary>
    /// <param name="pairs">Matched chapter pairs to compare.</param>
    /// <param name="similarityThreshold">
    /// 유사도 임계값. 기본값 0.3은 API 직접 호출 시 사용.
    /// CLI의 --threshold(기본 0.85)와 별개이며, Program.cs에서 전달됨.
    /// </param>
    /// <returns>Diff items describing added, deleted, or modified content.</returns>
    public static List<DiffItem> ComputeDiffs(IReadOnlyList<ChapterPair> pairs, double similarityThreshold = 0.3)
    {
        if (pairs is null)
        {
            throw new ArgumentNullException(nameof(pairs));
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

    public static List<DiffItem> ComputeDiffs(ChapterMatchResult matchResult, double similarityThreshold = 0.3)
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
        var sourceParagraphs = SplitParagraphs(source.Content.ToString());
        var targetParagraphs = SplitParagraphs(target.Content.ToString());

        if (sourceParagraphs.Length == 0 && targetParagraphs.Length == 0)
        {
            return;
        }

        var sourceText = string.Join("\n\n", sourceParagraphs);
        var targetText = string.Join("\n\n", targetParagraphs);
        var diffResult = Differ.CreateDiffs(
            sourceText,
            targetText,
            ignoreWhiteSpace: true,
            ignoreCase: false,
            chunker: ParagraphChunker.Instance);

        foreach (var block in diffResult.DiffBlocks)
        {
            var deleted = TakeRange(sourceParagraphs, block.DeleteStartA, block.DeleteCountA);
            var inserted = TakeRange(targetParagraphs, block.InsertStartB, block.InsertCountB);
            var pairedCount = Math.Min(deleted.Count, inserted.Count);

            for (var i = 0; i < pairedCount; i++)
            {
                var before = deleted[i];
                var after = inserted[i];
                var similarity = SimilarityCalculator.Calculate(before, after);

                if (similarity >= similarityThreshold)
                {
                    destination.Add(CreateItem(
                        source.Key,
                        ChangeType.Modified,
                        before,
                        after,
                        similarity,
                        BuildPageRef(source, target)));
                }
                else
                {
                    destination.Add(CreateItem(
                        source.Key,
                        ChangeType.Deleted,
                        before,
                        string.Empty,
                        similarity,
                        BuildPageRef(source, target)));
                    destination.Add(CreateItem(
                        target.Key,
                        ChangeType.Added,
                        string.Empty,
                        after,
                        similarity,
                        BuildPageRef(source, target)));
                }
            }

            for (var i = pairedCount; i < deleted.Count; i++)
            {
                destination.Add(CreateItem(
                    source.Key,
                    ChangeType.Deleted,
                    deleted[i],
                    string.Empty,
                    0d,
                    BuildPageRef(source, target)));
            }

            for (var i = pairedCount; i < inserted.Count; i++)
            {
                destination.Add(CreateItem(
                    target.Key,
                    ChangeType.Added,
                    string.Empty,
                    inserted[i],
                    0d,
                    BuildPageRef(source, target)));
            }
        }
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
            PageRef = pageRef
        };
    }

    private static string BuildPageRef(ChapterNode? source, ChapterNode? target)
    {
        var start = source?.PageStart ?? target?.PageStart ?? 0;
        var end = target?.PageEnd ?? source?.PageEnd ?? start;

        if (start <= 0 && end <= 0)
        {
            return string.Empty;
        }

        if (end <= 0)
        {
            end = start;
        }

        return $"p{start}-{end}";
    }

    private static List<string> TakeRange(string[] values, int start, int count)
    {
        if (count <= 0 || start < 0 || start >= values.Length)
        {
            return new List<string>();
        }

        var maxCount = Math.Min(count, values.Length - start);
        return new List<string>(new ArraySegment<string>(values, start, maxCount));
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

    private sealed class ParagraphChunker : IChunker
    {
        public static readonly ParagraphChunker Instance = new();

        public IReadOnlyList<string> Chunk(string text)
        {
            return SplitParagraphs(text);
        }
    }
}
