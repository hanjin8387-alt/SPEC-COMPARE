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
        double titleSimilarityThreshold = 0.7)
    {
        if (sourceRoots is null)
        {
            throw new ArgumentNullException(nameof(sourceRoots));
        }

        if (targetRoots is null)
        {
            throw new ArgumentNullException(nameof(targetRoots));
        }

        var source = Flatten(sourceRoots);
        var target = Flatten(targetRoots);
        var result = new ChapterMatchResult();
        var matchedTargetKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sourceNode in source)
        {
            var exactTarget = target.FirstOrDefault(node =>
                !matchedTargetKeys.Contains(node.Key) &&
                node.Key.Equals(sourceNode.Key, StringComparison.Ordinal));

            if (exactTarget is null)
            {
                continue;
            }

            result.Matches.Add(new ChapterPair(sourceNode, exactTarget, 1.0));
            matchedTargetKeys.Add(exactTarget.Key);
        }

        var unmatchedSource = source
            .Where(node => result.Matches.All(match => match.Source?.Key != node.Key))
            .ToList();

        var unmatchedTarget = target
            .Where(node => !matchedTargetKeys.Contains(node.Key))
            .ToList();

        foreach (var sourceNode in unmatchedSource)
        {
            var bestCandidate = unmatchedTarget
                .Select(targetNode => new
                {
                    Node = targetNode,
                    Score = CalculateSimilarity(sourceNode.Title, targetNode.Title)
                })
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (bestCandidate is not null && bestCandidate.Score >= titleSimilarityThreshold)
            {
                result.Matches.Add(new ChapterPair(sourceNode, bestCandidate.Node, bestCandidate.Score));
                unmatchedTarget.Remove(bestCandidate.Node);
                continue;
            }

            result.UnmatchedSource.Add(sourceNode);
        }

        result.UnmatchedTarget.AddRange(unmatchedTarget);
        return result;
    }

    private static List<ChapterNode> Flatten(IReadOnlyList<ChapterNode> roots)
    {
        var result = new List<ChapterNode>();
        foreach (var node in roots)
        {
            result.Add(node);
            if (node.Children.Count > 0)
            {
                result.AddRange(Flatten(node.Children));
            }
        }

        return result;
    }

    private static double CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return 1.0;
        }

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0.0;
        }

        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var normalizedLeft = left.Trim();
        var normalizedRight = right.Trim();
        return SimilarityCalculator.Calculate(normalizedLeft, normalizedRight, ignoreCase: true);
    }
}
