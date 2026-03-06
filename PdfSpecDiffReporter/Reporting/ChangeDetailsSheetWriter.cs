using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class ChangeDetailsSheetWriter
{
    private static readonly string[] Headers =
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

    public void Write(XLWorkbook workbook, ExcelReportContext context, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("ChangeDetails");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        for (var index = 0; index < context.OrderedDiffs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = context.OrderedDiffs[index];
            var rowNumber = index + 2;
            var diffId = $"D{index + 1:0000}";
            var chapterKey = item.ChapterKey ?? string.Empty;

            sheet.Cell(rowNumber, 1).Value = diffId;
            sheet.Cell(rowNumber, 2).Value = chapterKey;
            sheet.Cell(rowNumber, 3).Value = context.TitleByKey.TryGetValue(chapterKey, out var title) ? title : string.Empty;
            sheet.Cell(rowNumber, 4).Value = item.ChangeType.ToString();
            sheet.Cell(rowNumber, 5).Value = ExcelReportSheetFormatter.CreatePreview(item.BeforeText, context.ReportOptions.PreviewTextLength);
            sheet.Cell(rowNumber, 6).Value = ExcelReportSheetFormatter.CreatePreview(item.AfterText, context.ReportOptions.PreviewTextLength);
            sheet.Cell(rowNumber, 7).Value = ExcelReportSheetFormatter.NormalizeSimilarity(item.SimilarityScore);
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
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(3), 45);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(5), 70);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(6), 70);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(8), 35);
        ExcelReportSheetFormatter.PrepareSheet(sheet);
    }
}
