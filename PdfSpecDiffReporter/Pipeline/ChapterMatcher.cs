using System;
using System.Collections.Generic;
using System.Linq;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class ChapterMatcher
{
    public static ChapterMatchResult Match(
        IReadOnlyList<ChapterNode> sourceRoots,
        IReadOnlyList<ChapterNode> targetRoots,
        double matchThreshold = 0.7d,
        CancellationToken cancellationToken = default)
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
        if (source.Count == 0 && target.Count == 0)
        {
            return new ChapterMatchResult(Array.Empty<ChapterPair>(), Array.Empty<ChapterNode>(), Array.Empty<ChapterNode>());
        }

        var matches = new List<MatchCandidate>();
        var matchedSource = new HashSet<int>();
        var matchedTarget = new HashSet<int>();

        AddCandidates(
            matches,
            BuildExactKeyAnchors(source, target, matchThreshold, cancellationToken),
            matchedSource,
            matchedTarget);

        AddCandidates(
            matches,
            BuildNearExactAnchors(source, target, matchedSource, matchedTarget, matchThreshold, cancellationToken),
            matchedSource,
            matchedTarget);

        AddCandidates(
            matches,
            AssignRemaining(source, target, matchedSource, matchedTarget, matchThreshold, cancellationToken),
            matchedSource,
            matchedTarget);

        var orderedMatches = matches
            .OrderBy(candidate => candidate.Source.Index)
            .ThenBy(candidate => candidate.Target.Index)
            .Select(candidate => new ChapterPair(candidate.Source.Node, candidate.Target.Node, candidate.Evidence))
            .ToArray();

        var unmatchedSource = source
            .Where(node => !matchedSource.Contains(node.Index))
            .Select(node => node.Node)
            .ToArray();

        var unmatchedTarget = target
            .Where(node => !matchedTarget.Contains(node.Index))
            .Select(node => node.Node)
            .ToArray();

        return new ChapterMatchResult(orderedMatches, unmatchedSource, unmatchedTarget);
    }

    private static void AddCandidates(
        ICollection<MatchCandidate> destination,
        IEnumerable<MatchCandidate> candidates,
        ISet<int> matchedSource,
        ISet<int> matchedTarget)
    {
        foreach (var candidate in candidates)
        {
            if (matchedSource.Contains(candidate.Source.Index) || matchedTarget.Contains(candidate.Target.Index))
            {
                continue;
            }

            matchedSource.Add(candidate.Source.Index);
            matchedTarget.Add(candidate.Target.Index);
            destination.Add(candidate);
        }
    }

    private static IReadOnlyList<MatchCandidate> BuildExactKeyAnchors(
        IReadOnlyList<IndexedNode> source,
        IReadOnlyList<IndexedNode> target,
        double matchThreshold,
        CancellationToken cancellationToken)
    {
        var uniqueSource = source
            .Where(node => node.Node.MatchKey.Length > 0)
            .GroupBy(node => node.Node.MatchKey, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var uniqueTarget = target
            .Where(node => node.Node.MatchKey.Length > 0)
            .GroupBy(node => node.Node.MatchKey, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var anchors = new List<MatchCandidate>();
        foreach (var entry in uniqueSource)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!uniqueTarget.TryGetValue(entry.Key, out var targetNode))
            {
                continue;
            }

            var candidate = BuildCandidate(
                entry.Value,
                targetNode,
                ChapterMatchKind.ExactKeyAnchor,
                source.Count,
                target.Count,
                "unique exact chapter key");

            if (candidate.Scores.OverallScore < Math.Max(matchThreshold, 0.60d) &&
                candidate.Scores.TitleScore < 0.35d)
            {
                continue;
            }

            anchors.Add(candidate);
        }

        return anchors
            .OrderByDescending(candidate => candidate.Scores.OverallScore)
            .ThenBy(candidate => Math.Abs(candidate.Source.Index - candidate.Target.Index))
            .ToArray();
    }

    private static IReadOnlyList<MatchCandidate> BuildNearExactAnchors(
        IReadOnlyList<IndexedNode> source,
        IReadOnlyList<IndexedNode> target,
        ISet<int> matchedSource,
        ISet<int> matchedTarget,
        double matchThreshold,
        CancellationToken cancellationToken)
    {
        var remainingSource = source.Where(node => !matchedSource.Contains(node.Index)).ToList();
        var remainingTarget = target.Where(node => !matchedTarget.Contains(node.Index)).ToList();

        var uniqueSource = remainingSource
            .Where(node => node.NormalizedTitle.Length > 0)
            .GroupBy(node => node.NormalizedTitle, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var uniqueTarget = remainingTarget
            .Where(node => node.NormalizedTitle.Length > 0)
            .GroupBy(node => node.NormalizedTitle, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var anchors = new List<MatchCandidate>();
        foreach (var entry in uniqueSource)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!uniqueTarget.TryGetValue(entry.Key, out var targetNode))
            {
                continue;
            }

            var candidate = BuildCandidate(
                entry.Value,
                targetNode,
                ChapterMatchKind.NearExactAnchor,
                remainingSource.Count,
                remainingTarget.Count,
                "unique normalized title anchor");

            if (candidate.Scores.TitleScore < 0.95d ||
                candidate.Scores.LevelScore < 0.60d ||
                candidate.Scores.OverallScore < Math.Max(matchThreshold, 0.75d))
            {
                continue;
            }

            anchors.Add(candidate);
        }

        return anchors
            .OrderByDescending(candidate => candidate.Scores.OverallScore)
            .ThenBy(candidate => Math.Abs(candidate.Source.Index - candidate.Target.Index))
            .ToArray();
    }

    private static IReadOnlyList<MatchCandidate> AssignRemaining(
        IReadOnlyList<IndexedNode> source,
        IReadOnlyList<IndexedNode> target,
        ISet<int> matchedSource,
        ISet<int> matchedTarget,
        double matchThreshold,
        CancellationToken cancellationToken)
    {
        var remainingSource = source.Where(node => !matchedSource.Contains(node.Index)).ToList();
        var remainingTarget = target.Where(node => !matchedTarget.Contains(node.Index)).ToList();
        if (remainingSource.Count == 0 || remainingTarget.Count == 0)
        {
            return Array.Empty<MatchCandidate>();
        }

        var size = Math.Max(remainingSource.Count, remainingTarget.Count);
        var weights = new double[size, size];
        var candidates = new Dictionary<(int SourceIndex, int TargetIndex), MatchCandidate>();

        for (var sourceIndex = 0; sourceIndex < remainingSource.Count; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var targetIndex = 0; targetIndex < remainingTarget.Count; targetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = BuildCandidate(
                    remainingSource[sourceIndex],
                    remainingTarget[targetIndex],
                    ChapterMatchKind.WeightedAssignment,
                    remainingSource.Count,
                    remainingTarget.Count,
                    "weighted assignment");

                if (candidate.Scores.OverallScore < matchThreshold)
                {
                    continue;
                }

                weights[sourceIndex, targetIndex] = candidate.Scores.OverallScore;
                candidates[(sourceIndex, targetIndex)] = candidate;
            }
        }

        var assignments = SolveAssignment(weights);
        var selected = new List<MatchCandidate>();
        for (var sourceIndex = 0; sourceIndex < remainingSource.Count; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetIndex = assignments[sourceIndex];
            if (targetIndex < 0 || targetIndex >= remainingTarget.Count)
            {
                continue;
            }

            if (candidates.TryGetValue((sourceIndex, targetIndex), out var candidate))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static MatchCandidate BuildCandidate(
        IndexedNode source,
        IndexedNode target,
        ChapterMatchKind kind,
        int sourceCount,
        int targetCount,
        string primaryReason)
    {
        var scores = CalculateScores(source, target, sourceCount, targetCount);
        var evidence = new ChapterMatchEvidence(
            kind,
            scores.OverallScore,
            scores.KeyScore,
            scores.TitleScore,
            scores.LevelScore,
            scores.OrderScore,
            scores.ContextScore,
            BuildReasons(scores, primaryReason));

        return new MatchCandidate(source, target, scores, evidence);
    }

    private static MatchScores CalculateScores(IndexedNode source, IndexedNode target, int sourceCount, int targetCount)
    {
        var keyScore = CalculateKeyScore(source.Node.MatchKey, target.Node.MatchKey);
        var titleScore = SimilarityCalculator.CalculateHybrid(source.Node.Title, target.Node.Title, ignoreCase: true);
        var levelScore = CalculateLevelScore(source.Node.Level, target.Node.Level);
        var orderScore = CalculateOrderScore(source.Index, target.Index, sourceCount, targetCount);
        var contextScore = CalculateContextScore(source, target);
        var overall = (keyScore * 0.30d) +
                      (titleScore * 0.28d) +
                      (levelScore * 0.10d) +
                      (orderScore * 0.10d) +
                      (contextScore * 0.22d);

        if (keyScore == 1d && titleScore >= 0.85d)
        {
            overall = Math.Max(overall, 0.97d);
        }

        return new MatchScores(
            Math.Clamp(overall, 0d, 1d),
            keyScore,
            titleScore,
            levelScore,
            orderScore,
            contextScore);
    }

    private static double CalculateKeyScore(string sourceKey, string targetKey)
    {
        var normalizedSource = sourceKey?.Trim() ?? string.Empty;
        var normalizedTarget = targetKey?.Trim() ?? string.Empty;

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
            return 0.8d;
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

    private static double CalculateOrderScore(int sourceIndex, int targetIndex, int sourceCount, int targetCount)
    {
        if (sourceCount <= 1 && targetCount <= 1)
        {
            return 1d;
        }

        var normalizedSource = sourceCount <= 1 ? 0d : (double)sourceIndex / (sourceCount - 1);
        var normalizedTarget = targetCount <= 1 ? 0d : (double)targetIndex / (targetCount - 1);
        return Math.Clamp(1d - Math.Abs(normalizedSource - normalizedTarget), 0d, 1d);
    }

    private static double CalculateContextScore(IndexedNode source, IndexedNode target)
    {
        var parentKeyScore = CalculateOptionalScore(source.Node.ParentMatchKey, target.Node.ParentMatchKey, CalculateKeyScore);
        var parentTitleScore = CalculateOptionalScore(
            source.Node.ParentTitle,
            target.Node.ParentTitle,
            static (left, right) => SimilarityCalculator.CalculateHybrid(left, right, ignoreCase: true));
        var previousScore = CalculateOptionalScore(
            source.PreviousTitle,
            target.PreviousTitle,
            static (left, right) => SimilarityCalculator.CalculateHybrid(left, right, ignoreCase: true));
        var nextScore = CalculateOptionalScore(
            source.NextTitle,
            target.NextTitle,
            static (left, right) => SimilarityCalculator.CalculateHybrid(left, right, ignoreCase: true));

        var parentScore = (parentKeyScore * 0.3d) + (parentTitleScore * 0.7d);
        var neighborScore = (previousScore + nextScore) / 2d;
        return Math.Clamp((parentScore * 0.85d) + (neighborScore * 0.15d), 0d, 1d);
    }

    private static double CalculateOptionalScore(
        string left,
        string right,
        Func<string, string, double> scorer)
    {
        var hasLeft = !string.IsNullOrWhiteSpace(left);
        var hasRight = !string.IsNullOrWhiteSpace(right);
        if (!hasLeft && !hasRight)
        {
            return 1d;
        }

        if (!hasLeft || !hasRight)
        {
            return 0.25d;
        }

        return scorer(left, right);
    }

    private static IReadOnlyList<string> BuildReasons(MatchScores scores, string primaryReason)
    {
        var reasons = new List<string> { primaryReason };

        if (scores.KeyScore == 1d)
        {
            reasons.Add("normalized keys matched exactly");
        }
        else if (scores.KeyScore >= 0.75d)
        {
            reasons.Add("numbering hierarchy aligned");
        }

        if (scores.TitleScore >= 0.95d)
        {
            reasons.Add("titles were nearly identical");
        }
        else if (scores.TitleScore >= 0.80d)
        {
            reasons.Add("titles were strongly similar");
        }

        if (scores.LevelScore == 1d)
        {
            reasons.Add("heading level matched");
        }

        if (scores.OrderScore >= 0.90d)
        {
            reasons.Add("relative order aligned");
        }

        if (scores.ContextScore >= 0.80d)
        {
            reasons.Add("parent or neighboring context aligned");
        }

        return reasons;
    }

    private static IReadOnlyList<IndexedNode> Flatten(IReadOnlyList<ChapterNode> roots)
    {
        var orderedNodes = new List<ChapterNode>();

        void Traverse(ChapterNode node)
        {
            orderedNodes.Add(node);
            foreach (var child in node.Children)
            {
                Traverse(child);
            }
        }

        foreach (var root in roots)
        {
            Traverse(root);
        }

        var result = new IndexedNode[orderedNodes.Count];
        for (var index = 0; index < orderedNodes.Count; index++)
        {
            var previousTitle = index > 0 ? NormalizeTitleForMatch(orderedNodes[index - 1].Title) : string.Empty;
            var nextTitle = index < orderedNodes.Count - 1 ? NormalizeTitleForMatch(orderedNodes[index + 1].Title) : string.Empty;
            result[index] = new IndexedNode(
                index,
                orderedNodes[index],
                NormalizeTitleForMatch(orderedNodes[index].Title),
                previousTitle,
                nextTitle);
        }

        return result;
    }

    private static string NormalizeTitleForMatch(string title)
    {
        return string.Join(' ', TextUtilities.Tokenize(title));
    }

    private static int[] SolveAssignment(double[,] weights)
    {
        var size = weights.GetLength(0);
        var potentialRows = new double[size + 1];
        var potentialColumns = new double[size + 1];
        var matching = new int[size + 1];
        var way = new int[size + 1];

        for (var row = 1; row <= size; row++)
        {
            matching[0] = row;
            var column = 0;
            var minimum = new double[size + 1];
            var used = new bool[size + 1];
            Array.Fill(minimum, double.PositiveInfinity);

            do
            {
                used[column] = true;
                var matchedRow = matching[column];
                var nextColumn = 0;
                var delta = double.PositiveInfinity;

                for (var currentColumn = 1; currentColumn <= size; currentColumn++)
                {
                    if (used[currentColumn])
                    {
                        continue;
                    }

                    var current = -weights[matchedRow - 1, currentColumn - 1] - potentialRows[matchedRow] - potentialColumns[currentColumn];
                    if (current < minimum[currentColumn])
                    {
                        minimum[currentColumn] = current;
                        way[currentColumn] = column;
                    }

                    if (minimum[currentColumn] < delta)
                    {
                        delta = minimum[currentColumn];
                        nextColumn = currentColumn;
                    }
                }

                for (var currentColumn = 0; currentColumn <= size; currentColumn++)
                {
                    if (used[currentColumn])
                    {
                        potentialRows[matching[currentColumn]] += delta;
                        potentialColumns[currentColumn] -= delta;
                    }
                    else
                    {
                        minimum[currentColumn] -= delta;
                    }
                }

                column = nextColumn;
            }
            while (matching[column] != 0);

            do
            {
                var previousColumn = way[column];
                matching[column] = matching[previousColumn];
                column = previousColumn;
            }
            while (column != 0);
        }

        var assignments = Enumerable.Repeat(-1, size).ToArray();
        for (var column = 1; column <= size; column++)
        {
            if (matching[column] != 0)
            {
                assignments[matching[column] - 1] = column - 1;
            }
        }

        return assignments;
    }

    private readonly record struct IndexedNode(
        int Index,
        ChapterNode Node,
        string NormalizedTitle,
        string PreviousTitle,
        string NextTitle);

    private readonly record struct MatchCandidate(
        IndexedNode Source,
        IndexedNode Target,
        MatchScores Scores,
        ChapterMatchEvidence Evidence);

    private readonly record struct MatchScores(
        double OverallScore,
        double KeyScore,
        double TitleScore,
        double LevelScore,
        double OrderScore,
        double ContextScore);
}
