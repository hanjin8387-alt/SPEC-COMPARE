using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class ChapterMatcher
{
    private static readonly Regex DuplicateSuffixPattern =
        new(@"_dup\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ChapterMatchResult Match(
        IReadOnlyList<ChapterNode> sourceRoots,
        IReadOnlyList<ChapterNode> targetRoots,
        double matchThreshold = 0.7d)
    {
        if (sourceRoots is null)
        {
            throw new ArgumentNullException(nameof(sourceRoots));
        }

        if (targetRoots is null)
        {
            throw new ArgumentNullException(nameof(targetRoots));
        }

        if (matchThreshold <= 0d || matchThreshold > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(matchThreshold));
        }

        var source = Flatten(sourceRoots);
        var target = Flatten(targetRoots);
        var result = new ChapterMatchResult();

        if (source.Count == 0 && target.Count == 0)
        {
            return result;
        }

        var candidates = BuildCandidates(source, target, matchThreshold);
        var matchedSource = new HashSet<int>();
        var matchedTarget = new HashSet<int>();

        foreach (var candidate in candidates)
        {
            if (!matchedSource.Add(candidate.Source.Index))
            {
                continue;
            }

            if (!matchedTarget.Add(candidate.Target.Index))
            {
                matchedSource.Remove(candidate.Source.Index);
                continue;
            }

            result.Matches.Add(new ChapterPair(
                candidate.Source.Node,
                candidate.Target.Node,
                candidate.TitleScore));
        }

        foreach (var sourceNode in source)
        {
            if (!matchedSource.Contains(sourceNode.Index))
            {
                result.UnmatchedSource.Add(sourceNode.Node);
            }
        }

        foreach (var targetNode in target)
        {
            if (!matchedTarget.Contains(targetNode.Index))
            {
                result.UnmatchedTarget.Add(targetNode.Node);
            }
        }

        return result;
    }

    private static List<MatchCandidate> BuildCandidates(
        IReadOnlyList<IndexedNode> source,
        IReadOnlyList<IndexedNode> target,
        double matchThreshold)
    {
        var candidates = new List<MatchCandidate>(source.Count * Math.Max(1, target.Count));
        foreach (var sourceNode in source)
        {
            foreach (var targetNode in target)
            {
                var titleScore = CalculateSimilarity(sourceNode.Node.Title, targetNode.Node.Title);
                var keyScore = CalculateKeyScore(sourceNode.Node.Key, targetNode.Node.Key);
                var levelScore = CalculateLevelScore(sourceNode.Node.Level, targetNode.Node.Level);
                var overall = (keyScore * 0.55d) + (titleScore * 0.40d) + (levelScore * 0.05d);

                if (overall < matchThreshold)
                {
                    continue;
                }

                candidates.Add(new MatchCandidate(sourceNode, targetNode, overall, titleScore, keyScore));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.OverallScore)
            .ThenByDescending(candidate => candidate.KeyScore)
            .ThenByDescending(candidate => candidate.TitleScore)
            .ThenBy(candidate => Math.Abs(candidate.Source.Index - candidate.Target.Index))
            .ThenBy(candidate => candidate.Source.Index)
            .ThenBy(candidate => candidate.Target.Index)
            .ToList();
    }

    private static List<IndexedNode> Flatten(IReadOnlyList<ChapterNode> roots)
    {
        var result = new List<IndexedNode>();
        var index = 0;

        void Traverse(ChapterNode node)
        {
            result.Add(new IndexedNode(index++, node));
            foreach (var child in node.Children)
            {
                Traverse(child);
            }
        }

        foreach (var root in roots)
        {
            Traverse(root);
        }

        return result;
    }

    private static double CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return 1.0d;
        }

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0.0d;
        }

        return SimilarityCalculator.Calculate(left.Trim(), right.Trim(), ignoreCase: true);
    }

    private static double CalculateKeyScore(string sourceKey, string targetKey)
    {
        var normalizedSource = NormalizeKeyForMatch(sourceKey);
        var normalizedTarget = NormalizeKeyForMatch(targetKey);

        if (normalizedSource.Length == 0 || normalizedTarget.Length == 0)
        {
            return 0d;
        }

        if (normalizedSource.Equals(normalizedTarget, StringComparison.Ordinal))
        {
            return 1d;
        }

        if (normalizedSource.StartsWith(normalizedTarget + ".", StringComparison.Ordinal) ||
            normalizedTarget.StartsWith(normalizedSource + ".", StringComparison.Ordinal))
        {
            return 0.75d;
        }

        var sourceRoot = normalizedSource.Split('.', 2)[0];
        var targetRoot = normalizedTarget.Split('.', 2)[0];
        if (sourceRoot.Equals(targetRoot, StringComparison.Ordinal))
        {
            return 0.45d;
        }

        return 0d;
    }

    private static double CalculateLevelScore(int sourceLevel, int targetLevel)
    {
        var delta = Math.Abs(sourceLevel - targetLevel);
        return delta switch
        {
            0 => 1.0d,
            1 => 0.6d,
            2 => 0.3d,
            _ => 0d
        };
    }

    private static string NormalizeKeyForMatch(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var trimmed = key.Trim();
        return DuplicateSuffixPattern.Replace(trimmed, string.Empty);
    }

    private readonly record struct IndexedNode(int Index, ChapterNode Node);

    private readonly record struct MatchCandidate(
        IndexedNode Source,
        IndexedNode Target,
        double OverallScore,
        double TitleScore,
        double KeyScore);
}
