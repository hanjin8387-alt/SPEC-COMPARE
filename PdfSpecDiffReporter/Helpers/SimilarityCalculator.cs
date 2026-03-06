using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfSpecDiffReporter.Helpers;

public static class SimilarityCalculator
{
    public static double Calculate(string? left, string? right, bool ignoreCase = false)
    {
        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return 0.0;
        }

        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (left.Equals(right, comparison))
        {
            return 1.0;
        }

        if (ignoreCase)
        {
            left = left.ToUpperInvariant();
            right = right.ToUpperInvariant();
        }

        var distance = LevenshteinDistance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);
        if (maxLength == 0)
        {
            return 1.0;
        }

        var score = 1.0 - ((double)distance / maxLength);
        return Math.Clamp(score, 0.0, 1.0);
    }

    public static double CalculateHybrid(string? left, string? right, bool ignoreCase = false)
    {
        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return 0.0;
        }

        var normalizedLeft = PrepareText(left, ignoreCase);
        var normalizedRight = PrepareText(right, ignoreCase);
        if (normalizedLeft.Equals(normalizedRight, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var edit = Calculate(normalizedLeft, normalizedRight, ignoreCase: false);
        var token = CalculateTokenJaccard(normalizedLeft, normalizedRight);
        var ngram = CalculateCharacterNGramSimilarity(normalizedLeft, normalizedRight);
        var tokenCount = Math.Max(TextUtilities.Tokenize(normalizedLeft).Count, TextUtilities.Tokenize(normalizedRight).Count);
        var longForm = Math.Max(normalizedLeft.Length, normalizedRight.Length) > 80 || tokenCount > 12;

        var hybrid = longForm
            ? (edit * 0.35d) + (token * 0.40d) + (ngram * 0.25d)
            : (edit * 0.55d) + (token * 0.30d) + (ngram * 0.15d);

        return Math.Clamp(hybrid, 0.0, 1.0);
    }

    public static double CalculateTokenJaccard(string? left, string? right)
    {
        var leftTokens = TextUtilities.Tokenize(left);
        var rightTokens = TextUtilities.Tokenize(right);
        if (leftTokens.Count == 0 && rightTokens.Count == 0)
        {
            return 1.0;
        }

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0.0;
        }

        var leftSet = leftTokens.ToHashSet(StringComparer.Ordinal);
        var rightSet = rightTokens.ToHashSet(StringComparer.Ordinal);
        var intersection = leftSet.Intersect(rightSet, StringComparer.Ordinal).Count();
        var union = leftSet.Union(rightSet, StringComparer.Ordinal).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    internal static int LevenshteinDistance(string source, string target)
    {
        if (source.Length > target.Length)
        {
            (source, target) = (target, source);
        }

        var prevRow = new int[source.Length + 1];
        var currRow = new int[source.Length + 1];

        for (var i = 0; i <= source.Length; i++)
        {
            prevRow[i] = i;
        }

        for (var j = 1; j <= target.Length; j++)
        {
            currRow[0] = j;
            for (var i = 1; i <= source.Length; i++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                currRow[i] = Math.Min(
                    Math.Min(currRow[i - 1] + 1, prevRow[i] + 1),
                    prevRow[i - 1] + cost);
            }

            (prevRow, currRow) = (currRow, prevRow);
        }

        return prevRow[source.Length];
    }

    private static string PrepareText(string text, bool ignoreCase)
    {
        return ignoreCase ? text.ToUpperInvariant() : text;
    }

    private static double CalculateCharacterNGramSimilarity(string left, string right)
    {
        var gramSize = Math.Min(left.Length, right.Length) < 12 ? 2 : 3;
        var leftGrams = BuildCharacterNGrams(left, gramSize);
        var rightGrams = BuildCharacterNGrams(right, gramSize);
        if (leftGrams.Count == 0 && rightGrams.Count == 0)
        {
            return 1.0;
        }

        if (leftGrams.Count == 0 || rightGrams.Count == 0)
        {
            return 0.0;
        }

        var intersection = leftGrams.Intersect(rightGrams, StringComparer.Ordinal).Count();
        var union = leftGrams.Union(rightGrams, StringComparer.Ordinal).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static HashSet<string> BuildCharacterNGrams(string text, int gramSize)
    {
        var compact = string.Concat(text.Where(ch => !char.IsWhiteSpace(ch)));
        if (compact.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (compact.Length <= gramSize)
        {
            return new HashSet<string>(StringComparer.Ordinal) { compact };
        }

        var grams = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index <= compact.Length - gramSize; index++)
        {
            grams.Add(compact.Substring(index, gramSize));
        }

        return grams;
    }
}
