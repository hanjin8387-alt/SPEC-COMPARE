using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfSpecDiffReporter.Tests;

internal readonly record struct PdfTestLine(string Text, double FontSize = 12d, double LineHeight = 18d);

internal static class TestPdfFactory
{
    public static string CreatePdf(params PdfTestLine[][] pages)
    {
        if (pages is null || pages.Length == 0)
        {
            throw new ArgumentException("At least one page is required.", nameof(pages));
        }

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, BuildPdf(pages));
        return path;
    }

    private static byte[] BuildPdf(IReadOnlyList<PdfTestLine[]> pages)
    {
        var objects = new List<string>();
        var fontObjectNumber = 3 + (pages.Count * 2);

        objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(index => $"{3 + (index * 2)} 0 R"));
        objects.Add($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>\nendobj\n");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageObjectNumber = 3 + (pageIndex * 2);
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add(
                $"{pageObjectNumber} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>\nendobj\n");

            var stream = BuildContentStream(pages[pageIndex]);
            objects.Add(
                $"{contentObjectNumber} 0 obj\n<< /Length {stream.Length} >>\nstream\n{stream}\nendstream\nendobj\n");
        }

        objects.Add($"{fontObjectNumber} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(builder.Length);
            builder.Append(obj);
        }

        var xrefOffset = builder.Length;
        builder.Append("xref\n");
        builder.Append($"0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append($"{offset:0000000000} 00000 n \n");
        }

        builder.Append("trailer\n");
        builder.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append($"{xrefOffset}\n");
        builder.Append("%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string BuildContentStream(IReadOnlyList<PdfTestLine> lines)
    {
        var builder = new StringBuilder();
        builder.Append("BT\n");

        var firstLine = true;
        foreach (var line in lines)
        {
            var fontSize = line.FontSize.ToString(CultureInfo.InvariantCulture);
            if (firstLine)
            {
                builder.Append($"/F1 {fontSize} Tf\n");
                builder.Append("72 740 Td\n");
                firstLine = false;
            }
            else
            {
                builder.Append($"/F1 {fontSize} Tf\n");
                builder.Append($"0 -{line.LineHeight.ToString(CultureInfo.InvariantCulture)} Td\n");
            }

            builder.Append($"({Escape(line.Text)}) Tj\n");
        }

        builder.Append("ET");
        return builder.ToString();
    }

    private static string Escape(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
