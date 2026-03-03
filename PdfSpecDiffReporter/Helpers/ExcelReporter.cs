using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Helpers;

public static class ExcelReporter
{
    private static readonly string[] SummaryHeaders =
    {
        "Source File",
        "Target File",
        "Total Chapters",
        "Matched",
        "Modified",
        "Added",
        "Deleted",
        "Processing Time"
    };

    private static readonly string[] ChangeDetailHeaders =
    {
        "Chapter ID",
        "Section Title",
        "Change Type",
        "Before (Old)",
        "After (New)",
        "Similarity (%)",
        "Page Refs"
    };

    private static readonly string[] UnmatchedHeaders =
    {
        "Origin (Old/New)",
        "Chapter ID",
        "Title",
        "Status"
    };

    public static void Generate(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        List<ChapterPair> allPairs,
        List<DiffItem> diffs,
        TimeSpan processingTime)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path must not be null or whitespace.", nameof(outputPath));
        }

        if (allPairs is null)
        {
            throw new ArgumentNullException(nameof(allPairs));
        }

        if (diffs is null)
        {
            throw new ArgumentNullException(nameof(diffs));
        }

        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var directoryPath = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using var workbook = new XLWorkbook();
            BuildSummarySheet(workbook, sourceFileName, targetFileName, allPairs, diffs, processingTime);
            BuildChangeDetailsSheet(workbook, allPairs, diffs);
            BuildUnmatchedSheet(workbook, allPairs);

            using var fileStream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.SaveAs(fileStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
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

        var matched = allPairs.Count(pair => pair.Source is not null && pair.Target is not null);
        var added = diffs.Count(item => item.ChangeType == ChangeType.Added);
        var deleted = diffs.Count(item => item.ChangeType == ChangeType.Deleted);
        var modified = diffs.Count(item => item.ChangeType == ChangeType.Modified);

        sheet.Cell(2, 1).Value = sourceFileName ?? string.Empty;
        sheet.Cell(2, 2).Value = targetFileName ?? string.Empty;
        sheet.Cell(2, 3).Value = allPairs.Count;
        sheet.Cell(2, 4).Value = matched;
        sheet.Cell(2, 5).Value = modified;
        sheet.Cell(2, 6).Value = added;
        sheet.Cell(2, 7).Value = deleted;
        sheet.Cell(2, 8).Value = processingTime.ToString(@"mm\:ss\.fff");

        sheet.Columns(1, 2).Style.Alignment.WrapText = true;
        sheet.Columns().AdjustToContents();
        CapColumnWidth(sheet.Column(1), 60);
        CapColumnWidth(sheet.Column(2), 60);
    }

    private static void BuildChangeDetailsSheet(
        XLWorkbook workbook,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs)
    {
        var sheet = workbook.Worksheets.Add("ChangeDetails");
        WriteHeaders(sheet, ChangeDetailHeaders);

        var titleByKey = BuildTitleByKeyLookup(allPairs);

        for (var index = 0; index < diffs.Count; index++)
        {
            var item = diffs[index];
            var rowNumber = index + 2;
            var chapterKey = item.ChapterKey ?? string.Empty;

            sheet.Cell(rowNumber, 1).Value = chapterKey;
            sheet.Cell(rowNumber, 2).Value = ResolveTitle(titleByKey, chapterKey);
            sheet.Cell(rowNumber, 3).Value = item.ChangeType.ToString();
            sheet.Cell(rowNumber, 4).Value = TruncateForCell(item.BeforeText);
            sheet.Cell(rowNumber, 5).Value = TruncateForCell(item.AfterText);
            sheet.Cell(rowNumber, 6).Value = NormalizeSimilarity(item.SimilarityScore);
            sheet.Cell(rowNumber, 7).Value = TruncateForCell(item.PageRef);

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

        sheet.Column(6).Style.NumberFormat.Format = "0.0";
        sheet.Columns().AdjustToContents();

        ApplyWrappedColumn(sheet.Column(2), 45);
        ApplyWrappedColumn(sheet.Column(4), 70);
        ApplyWrappedColumn(sheet.Column(5), 70);
        ApplyWrappedColumn(sheet.Column(7), 35);
    }

    private static void BuildUnmatchedSheet(XLWorkbook workbook, IReadOnlyList<ChapterPair> allPairs)
    {
        var sheet = workbook.Worksheets.Add("Unmatched");
        WriteHeaders(sheet, UnmatchedHeaders);

        var rowNumber = 2;
        foreach (var pair in allPairs)
        {
            if (pair.Source is not null && pair.Target is null)
            {
                sheet.Cell(rowNumber, 1).Value = "Old";
                sheet.Cell(rowNumber, 2).Value = pair.Source.Key;
                sheet.Cell(rowNumber, 3).Value = pair.Source.Title;
                sheet.Cell(rowNumber, 4).Value = "Deleted (No Match in New)";
                sheet.Row(rowNumber).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
                rowNumber++;
            }
            else if (pair.Source is null && pair.Target is not null)
            {
                sheet.Cell(rowNumber, 1).Value = "New";
                sheet.Cell(rowNumber, 2).Value = pair.Target.Key;
                sheet.Cell(rowNumber, 3).Value = pair.Target.Title;
                sheet.Cell(rowNumber, 4).Value = "Added (No Match in Old)";
                sheet.Row(rowNumber).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
                rowNumber++;
            }
        }

        sheet.Columns().AdjustToContents();
        ApplyWrappedColumn(sheet.Column(3), 50);
        ApplyWrappedColumn(sheet.Column(4), 45);
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

    private static string TruncateForCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= Constants.MaxExcerptLength
            ? value
            : value[..Constants.MaxExcerptLength];
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

