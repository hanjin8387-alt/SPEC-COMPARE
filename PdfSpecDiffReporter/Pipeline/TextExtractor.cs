using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using UglyToad.PdfPig;

namespace PdfSpecDiffReporter.Pipeline;

public static class TextExtractor
{
    public static List<PageText> ExtractPages(MemoryStream pdfStream)
    {
        if (pdfStream is null)
        {
            throw new ArgumentNullException(nameof(pdfStream));
        }

        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        var pages = new List<PageText>();

        try
        {
            using var document = PdfDocument.Open(pdfStream);

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords()
                    .Select(word =>
                    {
                        var pointSize = word.Letters.FirstOrDefault()?.PointSize ?? 0d;

                        return new WordInfo(
                            word.Text,
                            word.BoundingBox.Left,
                            word.BoundingBox.Bottom,
                            pointSize);
                    })
                    .ToList();

                pages.Add(new PageText(
                    page.Number,
                    page.Text ?? string.Empty,
                    words));
            }

            return pages;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }
}
