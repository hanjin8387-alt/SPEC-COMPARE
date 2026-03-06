using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PdfSpecDiffReporter.Helpers;

internal static class TextUtilities
{
    private static readonly Regex TokenPattern =
        new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    public static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }

    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return TokenPattern.Matches(text)
            .Select(match => match.Value.ToUpperInvariant())
            .ToArray();
    }
}
