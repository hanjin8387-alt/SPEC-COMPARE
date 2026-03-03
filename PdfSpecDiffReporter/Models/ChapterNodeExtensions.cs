using System;

namespace PdfSpecDiffReporter.Models;

public static class ChapterNodeExtensions
{
    public static string GetPageRange(this ChapterNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var start = node.PageStart <= 0 ? 0 : node.PageStart;
        var end = node.PageEnd <= 0 ? start : node.PageEnd;
        return start <= 0 ? string.Empty : $"{start}-{end}";
    }
}
