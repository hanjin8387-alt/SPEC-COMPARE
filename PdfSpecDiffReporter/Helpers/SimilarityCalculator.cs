using System;

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
}
