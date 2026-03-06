using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Tests;

public sealed class ExcelReporterTests
{
    [Fact]
    public void Generate_CreatesStructuredWorkbookWithPreviewAndFullTextSheets()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        var before = new string('A', 620);
        var after = new string('B', 620);
        var pairs = new List<ChapterPair>
        {
            CreateMatchedPair("2", "2", "Details", 1),
            CreateMatchedPair("1", "1", "Overview", 0),
            new(CreateNode("3", "Appendix", order: 2), null, null)
        };

        var diffs = new List<DiffItem>
        {
            new("2", ChangeType.Modified, before, after, 0.83d, "p.2")
        };

        try
        {
            ExcelReporter.Generate(
                outputPath,
                "source.pdf",
                "target.pdf",
                pairs,
                diffs,
                () => TimeSpan.FromSeconds(2),
                cancellationToken: CancellationToken.None);

            using var workbook = new XLWorkbook(outputPath);
            var changeSheet = workbook.Worksheet("ChangeDetails");
            var fullTextSheet = workbook.Worksheet("FullText");
            var matchSheet = workbook.Worksheet("MatchEvidence");
            var unmatchedSheet = workbook.Worksheet("Unmatched");
            var diagnosticsSheet = workbook.Worksheet("Diagnostics");

            Assert.Equal(1, changeSheet.SheetView.SplitRow);
            Assert.True(changeSheet.AutoFilter.IsEnabled);
            Assert.True(matchSheet.AutoFilter.IsEnabled);
            Assert.True(unmatchedSheet.AutoFilter.IsEnabled);
            Assert.Equal("1", matchSheet.Cell(2, 1).GetString());
            Assert.Equal(before, fullTextSheet.Cell(2, 4).GetString());
            Assert.NotEqual(before, changeSheet.Cell(2, 5).GetString());
            Assert.Equal("Generated UTC", diagnosticsSheet.Cell(2, 1).GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static ChapterPair CreateMatchedPair(string sourceKey, string targetKey, string title, int order)
    {
        return new ChapterPair(
            CreateNode(sourceKey, title, order),
            CreateNode(targetKey, title, order),
            new ChapterMatchEvidence(
                ChapterMatchKind.ExactKeyAnchor,
                1d,
                1d,
                1d,
                1d,
                1d,
                1d,
                new[] { "exact key anchor" }));
    }

    private static ChapterNode CreateNode(string key, string title, int order)
    {
        return new ChapterNode
        {
            Key = key,
            MatchKey = key,
            Title = title,
            Level = 1,
            Content = $"{title} content",
            PageStart = order + 1,
            PageEnd = order + 1,
            Order = order
        };
    }
}
