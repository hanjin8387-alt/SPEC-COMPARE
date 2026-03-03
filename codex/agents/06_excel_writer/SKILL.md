---
name: excel-writer
description: >
  Phase 4b agent — generates the final .xlsx report with Summary, ChangeDetails,
  and Unmatched sheets using ClosedXML. All data written from memory only.
---

# Agent 06 — Excel Report Writer

## Scope

Generate a formatted Excel (.xlsx) workbook with three sheets from in-memory
diff results. This is the **only** component that writes to disk.

## Inputs

- `List<DiffItem>` from Agent 05
- `List<ChapterPair>` from Agent 04 (for unmatched chapters)
- Source/target file names, processing time
- Output file path (from CLI `--output` option)
- `codex/SKILL.md` for sheet definitions

## Outputs

- `Pipeline/ExcelReporter.cs` — ClosedXML-based report generator
- Valid `.xlsx` file with 3 sheets
- Unit tests

## Acceptance Criteria

1. **Sheet 1: Summary** — single-row summary with columns:
   `Source File | Target File | Total Chapters | Matched | Modified | Added | Deleted | Processing Time`
2. **Sheet 2: ChangeDetails** — one row per `DiffItem`:
   `Chapter ID | Section Title | Change Type | Before (Old) | After (New) | Similarity (%) | Page Refs`
3. **Sheet 3: Unmatched** — one row per unmatched chapter:
   `Origin (Old/New) | Chapter ID | Title | Status`
4. Color coding:
   - Added rows: light green background (`#C6EFCE`)
   - Deleted rows: light red background (`#FFC7CE`)
   - Modified rows: light yellow background (`#FFEB9C`)
5. Text wrapping enabled, column auto-fit, truncation at 500 chars
6. File written via `workbook.SaveAs(new FileStream(...))` — no temp files
7. `dotnet build --warnaserror` passes

## Detailed Instructions

### ExcelReporter.cs

```csharp
using ClosedXML.Excel;

public static class ExcelReporter
{
    public static void Generate(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        List<ChapterPair> allPairs,
        List<DiffItem> diffs,
        TimeSpan processingTime)
    {
        using var workbook = new XLWorkbook();

        // --- Sheet 1: Summary ---
        var summary = workbook.AddWorksheet("Summary");
        var summaryHeaders = new[]
        {
            "Source File", "Target File", "Total Chapters",
            "Matched", "Modified", "Added", "Deleted", "Processing Time"
        };
        for (int i = 0; i < summaryHeaders.Length; i++)
        {
            summary.Cell(1, i + 1).Value = summaryHeaders[i];
            summary.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var matched = allPairs.Count(p => p.Source != null && p.Target != null);
        var added = diffs.Count(d => d.ChangeType == ChangeType.Added);
        var deleted = diffs.Count(d => d.ChangeType == ChangeType.Deleted);
        var modified = diffs.Count(d => d.ChangeType == ChangeType.Modified);

        summary.Cell(2, 1).Value = sourceFileName;
        summary.Cell(2, 2).Value = targetFileName;
        summary.Cell(2, 3).Value = allPairs.Count;
        summary.Cell(2, 4).Value = matched;
        summary.Cell(2, 5).Value = modified;
        summary.Cell(2, 6).Value = added;
        summary.Cell(2, 7).Value = deleted;
        summary.Cell(2, 8).Value = processingTime.ToString(@"mm\:ss\.fff");

        summary.Columns().AdjustToContents();

        // --- Sheet 2: ChangeDetails ---
        var details = workbook.AddWorksheet("ChangeDetails");
        var detailHeaders = new[]
        {
            "Chapter ID", "Section Title", "Change Type",
            "Before (Old)", "After (New)", "Similarity (%)", "Page Refs"
        };
        for (int i = 0; i < detailHeaders.Length; i++)
        {
            details.Cell(1, i + 1).Value = detailHeaders[i];
            details.Cell(1, i + 1).Style.Font.Bold = true;
        }

        for (int row = 0; row < diffs.Count; row++)
        {
            var d = diffs[row];
            var r = row + 2;
            details.Cell(r, 1).Value = d.ChapterKey;
            details.Cell(r, 2).Value = GetChapterTitle(allPairs, d.ChapterKey);
            details.Cell(r, 3).Value = d.ChangeType.ToString();
            details.Cell(r, 4).Value = d.BeforeText;
            details.Cell(r, 5).Value = d.AfterText;
            details.Cell(r, 6).Value = Math.Round(d.SimilarityScore * 100, 1);
            details.Cell(r, 7).Value = d.PageRef;

            // Color code rows
            var bgColor = d.ChangeType switch
            {
                ChangeType.Added => XLColor.FromHtml("#C6EFCE"),
                ChangeType.Deleted => XLColor.FromHtml("#FFC7CE"),
                ChangeType.Modified => XLColor.FromHtml("#FFEB9C"),
                _ => XLColor.NoColor
            };
            details.Row(r).Style.Fill.BackgroundColor = bgColor;
        }

        details.Columns().AdjustToContents();
        details.Column(4).Width = 60; // Before
        details.Column(5).Width = 60; // After
        details.Columns(4, 5).Style.Alignment.WrapText = true;

        // --- Sheet 3: Unmatched ---
        var unmatched = workbook.AddWorksheet("Unmatched");
        var unmatchedHeaders = new[]
        {
            "Origin (Old/New)", "Chapter ID", "Title", "Status"
        };
        for (int i = 0; i < unmatchedHeaders.Length; i++)
        {
            unmatched.Cell(1, i + 1).Value = unmatchedHeaders[i];
            unmatched.Cell(1, i + 1).Style.Font.Bold = true;
        }

        int ur = 2;
        foreach (var pair in allPairs)
        {
            if (pair.Source != null && pair.Target == null)
            {
                unmatched.Cell(ur, 1).Value = "Old";
                unmatched.Cell(ur, 2).Value = pair.Source.Key;
                unmatched.Cell(ur, 3).Value = pair.Source.Title;
                unmatched.Cell(ur, 4).Value = "Deleted (No Match in New)";
                ur++;
            }
            else if (pair.Source == null && pair.Target != null)
            {
                unmatched.Cell(ur, 1).Value = "New";
                unmatched.Cell(ur, 2).Value = pair.Target.Key;
                unmatched.Cell(ur, 3).Value = pair.Target.Title;
                unmatched.Cell(ur, 4).Value = "Added (No Match in Old)";
                ur++;
            }
        }

        unmatched.Columns().AdjustToContents();

        // --- Save ---
        workbook.SaveAs(outputPath);
    }
}
```

## Edge Cases

- **No diffs at all** (identical documents): ChangeDetails sheet has only headers
- **No unmatched chapters**: Unmatched sheet has only headers
- **Very long file paths in summary**: Auto-fit handles it
- **Output path directory doesn't exist**: Create directory before saving
- **Output file already exists**: Overwrite silently (CLI convention)
- **ClosedXML max row limit**: ~1M rows; unlikely to hit but validate
- **Excel cell character limit (32,767 chars)**: Truncation at 500 ensures safety
- **Filename pattern**: default `Diff_Report_{yyyyMMdd_HHmmss}.xlsx`

## Never Do

- ❌ Write any file other than the final `.xlsx`
- ❌ Create temp files during Excel generation
- ❌ Log cell contents (document excerpts)
- ❌ Use network for any part of reporting
- ❌ Store the `XLWorkbook` object beyond the `using` scope

## Suggested Unit Tests

```csharp
[Fact]
public void Generate_CreatesFileWithThreeSheets()
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
    try
    {
        ExcelReporter.Generate(
            outputPath, "source.pdf", "target.pdf",
            new List<ChapterPair>(),
            new List<DiffItem>(),
            TimeSpan.FromSeconds(5)
        );

        Assert.True(File.Exists(outputPath));
        using var wb = new XLWorkbook(outputPath);
        Assert.Equal(3, wb.Worksheets.Count);
        Assert.NotNull(wb.Worksheet("Summary"));
        Assert.NotNull(wb.Worksheet("ChangeDetails"));
        Assert.NotNull(wb.Worksheet("Unmatched"));
    }
    finally { File.Delete(outputPath); }
}

[Fact]
public void Generate_SummaryHasCorrectCounts()
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
    var diffs = new List<DiffItem>
    {
        new() { ChangeType = ChangeType.Added, ChapterKey = "1" },
        new() { ChangeType = ChangeType.Added, ChapterKey = "2" },
        new() { ChangeType = ChangeType.Deleted, ChapterKey = "3" },
        new() { ChangeType = ChangeType.Modified, ChapterKey = "4" },
    };
    try
    {
        ExcelReporter.Generate(outputPath, "s.pdf", "t.pdf",
            new List<ChapterPair>(), diffs, TimeSpan.Zero);

        using var wb = new XLWorkbook(outputPath);
        var ws = wb.Worksheet("Summary");
        Assert.Equal(2, (int)ws.Cell(2, 6).Value); // Added
        Assert.Equal(1, (int)ws.Cell(2, 7).Value); // Deleted
        Assert.Equal(1, (int)ws.Cell(2, 5).Value); // Modified
    }
    finally { File.Delete(outputPath); }
}

[Fact]
public void Generate_ChangeDetailsColorCoded()
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
    var diffs = new List<DiffItem>
    {
        new() { ChangeType = ChangeType.Added, ChapterKey = "1" },
        new() { ChangeType = ChangeType.Deleted, ChapterKey = "2" },
    };
    try
    {
        ExcelReporter.Generate(outputPath, "s.pdf", "t.pdf",
            new List<ChapterPair>(), diffs, TimeSpan.Zero);

        using var wb = new XLWorkbook(outputPath);
        var ws = wb.Worksheet("ChangeDetails");
        // Row 2 = Added (green), Row 3 = Deleted (red)
        Assert.NotEqual(XLColor.NoColor, ws.Row(2).Style.Fill.BackgroundColor);
    }
    finally { File.Delete(outputPath); }
}
```
