using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfSpecDiffReporter.Helpers;

internal static class TextUtilities
{
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
}
