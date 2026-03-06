using ClosedXML.Excel;
using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class UnmatchedSheetWriter
{
    private static readonly string[] Headers =
    {
        "Origin",
        "Chapter ID",
        "Title",
        "Page Refs",
        "Status"
    };

    public void Write(XLWorkbook workbook, ExcelReportContext context, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("Unmatched");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        var unmatched = context.OrderedPairs
            .Where(pair => pair.Source is null || pair.Target is null)
            .OrderBy(pair => pair.Source?.Order ?? pair.Target?.Order ?? int.MaxValue)
            .ToArray();

        for (var index = 0; index < unmatched.Length; index++)
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
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(3), 50);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(5), 45);
        ExcelReportSheetFormatter.PrepareSheet(sheet);
    }
}
