using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Helpers;

public static class ExcelReporter
{
    private const int PreviewTextLength = 500;

    private static readonly string[] SummaryHeaders =
    {
        "Source File",
        "Target File",
        "Source Chapters",
        "Target Chapters",
        "Matched Chapters",
        "Unmatched Old Chapters",
        "Unmatched New Chapters",
        "Modified Items",
        "Added Items",
        "Deleted Items",
        "Processing Time"
    };

    private static readonly string[] ChangeDetailHeaders =
    {
        "Diff ID",
        "Chapter ID",
        "Section Title",
        "Change Type",
        "Before Preview",
        "After Preview",
        "Similarity (%)",
        "Page Refs"
    };

    private static readonly string[] FullTextHeaders =
    {
        "Diff ID",
        "Chapter ID",
        "Change Type",
        "Before Full Text",
        "After Full Text",
        "Page Refs"
    };

    private static readonly string[] MatchHeaders =
    {
        "Source ID",
        "Source Title",
        "Target ID",
        "Target Title",
        "Match Kind",
        "Overall Score",
        "Key Score",
        "Title Score",
        "Level Score",
        "Order Score",
        "Context Score",
        "Reasons"
    };

    private static readonly string[] UnmatchedHeaders =
    {
        "Origin",
        "Chapter ID",
        "Title",
        "Page Refs",
        "Status"
    };

    private static readonly string[] DiagnosticsHeaders =
    {
        "Key",
        "Value"
    };

    public static void Generate(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        Func<TimeSpan> processingTimeProvider,
        IReadOnlyList<KeyValuePair<string, string>>? diagnostics = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path must not be null or whitespace.", nameof(outputPath));
        }

        ArgumentNullException.ThrowIfNull(allPairs);
        ArgumentNullException.ThrowIfNull(diffs);
        ArgumentNullException.ThrowIfNull(processingTimeProvider);

        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var directoryPath = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var orderedPairs = OrderPairs(allPairs);
            var orderedDiffs = diffs.ToList();

            using var workbook = new XLWorkbook();
            BuildSummarySheet(workbook, sourceFileName, targetFileName, orderedPairs, orderedDiffs, processingTimeProvider());
            BuildChangeDetailsSheet(workbook, orderedPairs, orderedDiffs, cancellationToken);
            BuildFullTextSheet(workbook, orderedDiffs, cancellationToken);
            BuildMatchEvidenceSheet(workbook, orderedPairs, cancellationToken);
            BuildUnmatchedSheet(workbook, orderedPairs, cancellationToken);
            BuildDiagnosticsSheet(workbook, sourceFileName, targetFileName, orderedPairs, orderedDiffs, processingTimeProvider(), diagnostics);

            using var fileStream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.SaveAs(fileStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }

    private static IReadOnlyList<ChapterPair> OrderPairs(IReadOnlyList<ChapterPair> allPairs)
    {
        return allPairs
            .OrderBy(pair => pair.Source?.Order ?? int.MaxValue)
            .ThenBy(pair => pair.Target?.Order ?? int.MaxValue)
            .ThenBy(pair => pair.Source?.Key ?? pair.Target?.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static void BuildSummarySheet(
        XLWorkbook workbook,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        TimeSpan processingTime)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        WriteHeaders(sheet, SummaryHeaders);

        var sourceChapters = allPairs
            .Where(pair => pair.Source is not null)
            .Select(pair => pair.Source!)
            .Distinct()
            .Count();

        var targetChapters = allPairs
            .Where(pair => pair.Target is not null)
            .Select(pair => pair.Target!)
            .Distinct()
            .Count();

        var matched = allPairs.Count(pair => pair.Source is not null && pair.Target is not null);
        var unmatchedOld = allPairs.Count(pair => pair.Source is not null && pair.Target is null);
        var unmatchedNew = allPairs.Count(pair => pair.Source is null && pair.Target is not null);
        var added = diffs.Count(item => item.ChangeType == ChangeType.Added);
        var deleted = diffs.Count(item => item.ChangeType == ChangeType.Deleted);
        var modified = diffs.Count(item => item.ChangeType == ChangeType.Modified);

        sheet.Cell(2, 1).Value = sourceFileName ?? string.Empty;
        sheet.Cell(2, 2).Value = targetFileName ?? string.Empty;
        sheet.Cell(2, 3).Value = sourceChapters;
        sheet.Cell(2, 4).Value = targetChapters;
        sheet.Cell(2, 5).Value = matched;
        sheet.Cell(2, 6).Value = unmatchedOld;
        sheet.Cell(2, 7).Value = unmatchedNew;
        sheet.Cell(2, 8).Value = modified;
        sheet.Cell(2, 9).Value = added;
        sheet.Cell(2, 10).Value = deleted;
        sheet.Cell(2, 11).Value = processingTime.ToString(@"mm\:ss\.fff");

        sheet.Columns(1, 2).Style.Alignment.WrapText = true;
        sheet.Columns().AdjustToContents();
        CapColumnWidth(sheet.Column(1), 60);
        CapColumnWidth(sheet.Column(2), 60);
        PrepareSheet(sheet, filter: false);
    }

    private static void BuildChangeDetailsSheet(
        XLWorkbook workbook,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("ChangeDetails");
        WriteHeaders(sheet, ChangeDetailHeaders);

        var titleByKey = BuildTitleByKeyLookup(allPairs);

        for (var index = 0; index < diffs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = diffs[index];
            var rowNumber = index + 2;
            var diffId = $"D{index + 1:0000}";
            var chapterKey = item.ChapterKey ?? string.Empty;

            sheet.Cell(rowNumber, 1).Value = diffId;
            sheet.Cell(rowNumber, 2).Value = chapterKey;
            sheet.Cell(rowNumber, 3).Value = ResolveTitle(titleByKey, chapterKey);
            sheet.Cell(rowNumber, 4).Value = item.ChangeType.ToString();
            sheet.Cell(rowNumber, 5).Value = CreatePreview(item.BeforeText);
            sheet.Cell(rowNumber, 6).Value = CreatePreview(item.AfterText);
            sheet.Cell(rowNumber, 7).Value = NormalizeSimilarity(item.SimilarityScore);
            sheet.Cell(rowNumber, 8).Value = item.PageRef ?? string.Empty;

            var rowColor = item.ChangeType switch
            {
                ChangeType.Added => XLColor.FromHtml("#C6EFCE"),
                ChangeType.Deleted => XLColor.FromHtml("#FFC7CE"),
                ChangeType.Modified => XLColor.FromHtml("#FFEB9C"),
                _ => XLColor.NoColor
            };

            if (rowColor != XLColor.NoColor)
            {
                sheet.Row(rowNumber).Style.Fill.BackgroundColor = rowColor;
            }
        }

        sheet.Column(7).Style.NumberFormat.Format = "0.0";
        sheet.Columns().AdjustToContents();

        ApplyWrappedColumn(sheet.Column(3), 45);
        ApplyWrappedColumn(sheet.Column(5), 70);
        ApplyWrappedColumn(sheet.Column(6), 70);
        ApplyWrappedColumn(sheet.Column(8), 35);
        PrepareSheet(sheet);
    }

    private static void BuildFullTextSheet(
        XLWorkbook workbook,
        IReadOnlyList<DiffItem> diffs,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("FullText");
        WriteHeaders(sheet, FullTextHeaders);

        for (var index = 0; index < diffs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = diffs[index];
            var rowNumber = index + 2;

            sheet.Cell(rowNumber, 1).Value = $"D{index + 1:0000}";
            sheet.Cell(rowNumber, 2).Value = item.ChapterKey ?? string.Empty;
            sheet.Cell(rowNumber, 3).Value = item.ChangeType.ToString();
            sheet.Cell(rowNumber, 4).Value = item.BeforeText ?? string.Empty;
            sheet.Cell(rowNumber, 5).Value = item.AfterText ?? string.Empty;
            sheet.Cell(rowNumber, 6).Value = item.PageRef ?? string.Empty;
        }

        sheet.Columns().AdjustToContents();
        ApplyWrappedColumn(sheet.Column(4), 90);
        ApplyWrappedColumn(sheet.Column(5), 90);
        ApplyWrappedColumn(sheet.Column(6), 35);
        PrepareSheet(sheet);
    }

    private static void BuildMatchEvidenceSheet(
        XLWorkbook workbook,
        IReadOnlyList<ChapterPair> allPairs,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("MatchEvidence");
        WriteHeaders(sheet, MatchHeaders);

        var matchedPairs = allPairs
            .Where(pair => pair.Source is not null && pair.Target is not null)
            .ToList();

        for (var index = 0; index < matchedPairs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pair = matchedPairs[index];
            var evidence = pair.Evidence ?? ChapterMatchEvidence.None;
            var rowNumber = index + 2;

            sheet.Cell(rowNumber, 1).Value = pair.Source!.Key;
            sheet.Cell(rowNumber, 2).Value = pair.Source.Title;
            sheet.Cell(rowNumber, 3).Value = pair.Target!.Key;
            sheet.Cell(rowNumber, 4).Value = pair.Target.Title;
            sheet.Cell(rowNumber, 5).Value = evidence.Kind.ToString();
            sheet.Cell(rowNumber, 6).Value = NormalizeSimilarity(evidence.OverallScore);
            sheet.Cell(rowNumber, 7).Value = NormalizeSimilarity(evidence.KeyScore);
            sheet.Cell(rowNumber, 8).Value = NormalizeSimilarity(evidence.TitleScore);
            sheet.Cell(rowNumber, 9).Value = NormalizeSimilarity(evidence.LevelScore);
            sheet.Cell(rowNumber, 10).Value = NormalizeSimilarity(evidence.OrderScore);
            sheet.Cell(rowNumber, 11).Value = NormalizeSimilarity(evidence.ContextScore);
            sheet.Cell(rowNumber, 12).Value = string.Join("; ", evidence.Reasons);
        }

        sheet.Columns(6, 11).Style.NumberFormat.Format = "0.0";
        sheet.Columns().AdjustToContents();
        ApplyWrappedColumn(sheet.Column(2), 45);
        ApplyWrappedColumn(sheet.Column(4), 45);
        ApplyWrappedColumn(sheet.Column(12), 70);
        PrepareSheet(sheet);
    }

    private static void BuildUnmatchedSheet(
        XLWorkbook workbook,
        IReadOnlyList<ChapterPair> allPairs,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("Unmatched");
        WriteHeaders(sheet, UnmatchedHeaders);

        var unmatched = allPairs
            .Where(pair => pair.Source is null || pair.Target is null)
            .OrderBy(pair => pair.Source?.Order ?? pair.Target?.Order ?? int.MaxValue)
            .ToList();

        for (var index = 0; index < unmatched.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pair = unmatched[index];
            var rowNumber = index + 2;

            if (pair.Source is not null && pair.Target is null)
            {
                sheet.Cell(rowNumber, 1).Value = "Old";
                sheet.Cell(rowNumber, 2).Value = pair.Source.Key;
                sheet.Cell(rowNumber, 3).Value = pair.Source.Title;
                sheet.Cell(rowNumber, 4).Value = PageReferenceFormatter.Format(pair.Source.PageStart, pair.Source.PageEnd);
                sheet.Cell(rowNumber, 5).Value = "Deleted (No Match in New)";
                sheet.Row(rowNumber).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
            }
            else if (pair.Source is null && pair.Target is not null)
            {
                sheet.Cell(rowNumber, 1).Value = "New";
                sheet.Cell(rowNumber, 2).Value = pair.Target.Key;
                sheet.Cell(rowNumber, 3).Value = pair.Target.Title;
                sheet.Cell(rowNumber, 4).Value = PageReferenceFormatter.Format(pair.Target.PageStart, pair.Target.PageEnd);
                sheet.Cell(rowNumber, 5).Value = "Added (No Match in Old)";
                sheet.Row(rowNumber).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
            }
        }

        sheet.Columns().AdjustToContents();
        ApplyWrappedColumn(sheet.Column(3), 50);
        ApplyWrappedColumn(sheet.Column(5), 45);
        PrepareSheet(sheet);
    }

    private static void BuildDiagnosticsSheet(
        XLWorkbook workbook,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        TimeSpan processingTime,
        IReadOnlyList<KeyValuePair<string, string>>? diagnostics)
    {
        var sheet = workbook.Worksheets.Add("Diagnostics");
        WriteHeaders(sheet, DiagnosticsHeaders);

        var rows = new List<KeyValuePair<string, string>>
        {
            new("Generated UTC", DateTime.UtcNow.ToString("u")),
            new("Source File", sourceFileName ?? string.Empty),
            new("Target File", targetFileName ?? string.Empty),
            new("Matched Chapters", allPairs.Count(pair => pair.Source is not null && pair.Target is not null).ToString()),
            new("Unmatched Source Chapters", allPairs.Count(pair => pair.Source is not null && pair.Target is null).ToString()),
            new("Unmatched Target Chapters", allPairs.Count(pair => pair.Source is null && pair.Target is not null).ToString()),
            new("Modified Items", diffs.Count(item => item.ChangeType == ChangeType.Modified).ToString()),
            new("Added Items", diffs.Count(item => item.ChangeType == ChangeType.Added).ToString()),
            new("Deleted Items", diffs.Count(item => item.ChangeType == ChangeType.Deleted).ToString()),
            new("Processing Time", processingTime.ToString(@"mm\:ss\.fff"))
        };

        if (diagnostics is not null)
        {
            rows.AddRange(diagnostics);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].Key;
            sheet.Cell(index + 2, 2).Value = rows[index].Value;
        }

        sheet.Columns().AdjustToContents();
        ApplyWrappedColumn(sheet.Column(2), 80);
        PrepareSheet(sheet, filter: false);
    }

    private static Dictionary<string, string> BuildTitleByKeyLookup(IReadOnlyList<ChapterPair> allPairs)
    {
        var titleByKey = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in allPairs)
        {
            if (pair.Source is not null &&
                !string.IsNullOrWhiteSpace(pair.Source.Key) &&
                !titleByKey.ContainsKey(pair.Source.Key))
            {
                titleByKey[pair.Source.Key] = pair.Source.Title ?? string.Empty;
            }

            if (pair.Target is not null &&
                !string.IsNullOrWhiteSpace(pair.Target.Key) &&
                !titleByKey.ContainsKey(pair.Target.Key))
            {
                titleByKey[pair.Target.Key] = pair.Target.Title ?? string.Empty;
            }
        }

        return titleByKey;
    }

    private static string ResolveTitle(IReadOnlyDictionary<string, string> titleByKey, string chapterKey)
    {
        return titleByKey.TryGetValue(chapterKey, out var title)
            ? title
            : string.Empty;
    }

    private static double NormalizeSimilarity(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return 0d;
        }

        var percentage = Math.Round(score * 100d, 1, MidpointRounding.AwayFromZero);
        return Math.Clamp(percentage, 0d, 100d);
    }

    private static string CreatePreview(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= PreviewTextLength
            ? value
            : $"{value[..PreviewTextLength]}...";
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
        }
    }

    private static void PrepareSheet(IXLWorksheet sheet, bool filter = true)
    {
        sheet.SheetView.FreezeRows(1);
        if (filter && sheet.LastRowUsed() is not null && sheet.LastColumnUsed() is not null)
        {
            sheet.Range(1, 1, sheet.LastRowUsed()!.RowNumber(), sheet.LastColumnUsed()!.ColumnNumber()).SetAutoFilter();
        }
    }

    private static void ApplyWrappedColumn(IXLColumn column, double maxWidth)
    {
        column.Style.Alignment.WrapText = true;
        column.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        CapColumnWidth(column, maxWidth);
    }

    private static void CapColumnWidth(IXLColumn column, double maxWidth)
    {
        if (column.Width > maxWidth)
        {
            column.Width = maxWidth;
        }
    }
}
