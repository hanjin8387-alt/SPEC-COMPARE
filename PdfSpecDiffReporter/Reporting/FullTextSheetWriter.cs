using ClosedXML.Excel;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class FullTextSheetWriter
{
    private static readonly string[] Headers =
    {
        "Diff ID",
        "Chapter ID",
        "Change Type",
        "Before Full Text",
        "After Full Text",
        "Page Refs"
    };

    public void Write(XLWorkbook workbook, ExcelReportContext context, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("FullText");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        for (var index = 0; index < context.OrderedDiffs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = context.OrderedDiffs[index];
            var rowNumber = index + 2;

            sheet.Cell(rowNumber, 1).Value = $"D{index + 1:0000}";
            sheet.Cell(rowNumber, 2).Value = item.ChapterKey ?? string.Empty;
            sheet.Cell(rowNumber, 3).Value = item.ChangeType.ToString();
            sheet.Cell(rowNumber, 4).Value = item.BeforeText ?? string.Empty;
            sheet.Cell(rowNumber, 5).Value = item.AfterText ?? string.Empty;
            sheet.Cell(rowNumber, 6).Value = item.PageRef ?? string.Empty;
        }

        sheet.Columns().AdjustToContents();
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(4), 90);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(5), 90);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(6), 35);
        ExcelReportSheetFormatter.PrepareSheet(sheet);
    }
}
