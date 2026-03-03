using ClosedXML.Excel;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ExcelReporterTests
{
    [Fact]
    public void Generate_CreatesWorkbookWithExpectedSheetsAndSummary()
    {
        var tempDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(tempDirectory, "report.xlsx");

        try
        {
            var pairs = new List<ChapterPair>
            {
                new(CreateChapter("1", "Intro"), CreateChapter("1", "Intro"), 1d),
                new(CreateChapter("2", "Legacy"), null, 0d),
                new(null, CreateChapter("3", "New"), 0d)
            };
            var diffs = new List<DiffItem>
            {
                new()
                {
                    ChapterKey = "1",
                    ChangeType = ChangeType.Modified,
                    BeforeText = "old text",
                    AfterText = "new text",
                    SimilarityScore = 0.9,
                    PageRef = "p1-1"
                },
                new()
                {
                    ChapterKey = "2",
                    ChangeType = ChangeType.Deleted,
                    BeforeText = "removed text",
                    AfterText = string.Empty,
                    SimilarityScore = 0d,
                    PageRef = "p2-2"
                },
                new()
                {
                    ChapterKey = "3",
                    ChangeType = ChangeType.Added,
                    BeforeText = string.Empty,
                    AfterText = "added text",
                    SimilarityScore = 0d,
                    PageRef = "p3-3"
                }
            };

            ExcelReporter.Generate(
                outputPath,
                sourceFileName: "old.pdf",
                targetFileName: "new.pdf",
                allPairs: pairs,
                diffs: diffs,
                processingTime: TimeSpan.FromMilliseconds(12345));

            Assert.True(File.Exists(outputPath));

            using var workbook = new XLWorkbook(outputPath);
            Assert.NotNull(workbook.Worksheet("Summary"));
            Assert.NotNull(workbook.Worksheet("ChangeDetails"));
            Assert.NotNull(workbook.Worksheet("Unmatched"));

            var summary = workbook.Worksheet("Summary");
            Assert.Equal("old.pdf", summary.Cell(2, 1).GetString());
            Assert.Equal("new.pdf", summary.Cell(2, 2).GetString());
            Assert.Equal(3, summary.Cell(2, 3).GetValue<int>());
            Assert.Equal(1, summary.Cell(2, 4).GetValue<int>());
            Assert.Equal(1, summary.Cell(2, 5).GetValue<int>());
            Assert.Equal(1, summary.Cell(2, 6).GetValue<int>());
            Assert.Equal(1, summary.Cell(2, 7).GetValue<int>());
            Assert.Equal("00:12.345", summary.Cell(2, 8).GetString());
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Generate_TruncatesLongPageReferencesToMaxExcerptLength()
    {
        var tempDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(tempDirectory, "report.xlsx");
        var longPageRef = new string('X', 700);

        try
        {
            var pairs = new List<ChapterPair>
            {
                new(CreateChapter("1", "Intro"), CreateChapter("1", "Intro"), 1d)
            };
            var diffs = new List<DiffItem>
            {
                new()
                {
                    ChapterKey = "1",
                    ChangeType = ChangeType.Modified,
                    BeforeText = "before",
                    AfterText = "after",
                    SimilarityScore = 0.1234,
                    PageRef = longPageRef
                }
            };

            ExcelReporter.Generate(outputPath, "source.pdf", "target.pdf", pairs, diffs, TimeSpan.Zero);

            using var workbook = new XLWorkbook(outputPath);
            var details = workbook.Worksheet("ChangeDetails");
            var storedPageRef = details.Cell(2, 7).GetString();

            Assert.Equal(500, storedPageRef.Length);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Generate_ThrowsForBlankOutputPath()
    {
        var pairs = new List<ChapterPair>();
        var diffs = new List<DiffItem>();

        Assert.Throws<ArgumentException>(() => ExcelReporter.Generate("  ", "old.pdf", "new.pdf", pairs, diffs, TimeSpan.Zero));
    }

    [Fact]
    public void Generate_UnmatchedSheet_HasCorrectRowCount()
    {
        var tempDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(tempDirectory, "report.xlsx");

        try
        {
            var pairs = new List<ChapterPair>
            {
                new(CreateChapter("1", "Matched A"), CreateChapter("1", "Matched B"), 1d),
                new(CreateChapter("2", "Only Old"), null, 0d),
                new(CreateChapter("3", "Only Old 2"), null, 0d),
                new(null, CreateChapter("4", "Only New"), 0d)
            };

            ExcelReporter.Generate(outputPath, "source.pdf", "target.pdf", pairs, new List<DiffItem>(), TimeSpan.Zero);

            using var workbook = new XLWorkbook(outputPath);
            var unmatched = workbook.Worksheet("Unmatched");
            var lastUsedRow = unmatched.LastRowUsed()?.RowNumber() ?? 0;

            Assert.Equal(4, lastUsedRow);
            Assert.Equal("Old", unmatched.Cell(2, 1).GetString());
            Assert.Equal("Old", unmatched.Cell(3, 1).GetString());
            Assert.Equal("New", unmatched.Cell(4, 1).GetString());
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Generate_RowColors_MatchChangeType()
    {
        var tempDirectory = CreateTempDirectory();
        var outputPath = Path.Combine(tempDirectory, "report.xlsx");

        try
        {
            var pairs = new List<ChapterPair>
            {
                new(CreateChapter("1", "Title 1"), CreateChapter("1", "Title 1"), 1d),
                new(CreateChapter("2", "Title 2"), null, 0d),
                new(null, CreateChapter("3", "Title 3"), 0d)
            };
            var diffs = new List<DiffItem>
            {
                new() { ChapterKey = "3", ChangeType = ChangeType.Added, BeforeText = string.Empty, AfterText = "added", SimilarityScore = 0d, PageRef = "p1-1" },
                new() { ChapterKey = "2", ChangeType = ChangeType.Deleted, BeforeText = "deleted", AfterText = string.Empty, SimilarityScore = 0d, PageRef = "p2-2" },
                new() { ChapterKey = "1", ChangeType = ChangeType.Modified, BeforeText = "before", AfterText = "after", SimilarityScore = 0.8, PageRef = "p3-3" }
            };

            ExcelReporter.Generate(outputPath, "source.pdf", "target.pdf", pairs, diffs, TimeSpan.Zero);

            using var workbook = new XLWorkbook(outputPath);
            var details = workbook.Worksheet("ChangeDetails");

            Assert.Equal(XLColor.FromHtml("#C6EFCE").Color.ToArgb(), details.Row(2).Style.Fill.BackgroundColor.Color.ToArgb());
            Assert.Equal(XLColor.FromHtml("#FFC7CE").Color.ToArgb(), details.Row(3).Style.Fill.BackgroundColor.Color.ToArgb());
            Assert.Equal(XLColor.FromHtml("#FFEB9C").Color.ToArgb(), details.Row(4).Style.Fill.BackgroundColor.Color.ToArgb());
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static ChapterNode CreateChapter(string key, string title)
    {
        return new ChapterNode
        {
            Key = key,
            Title = title,
            Level = 1,
            PageStart = 1,
            PageEnd = 1
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        catch
        {
            // Cleanup best effort only for tests.
        }
    }
}
