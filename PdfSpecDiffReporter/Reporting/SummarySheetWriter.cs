using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class SummarySheetWriter
{
    private static readonly string[] Headers =
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

    public void Write(XLWorkbook workbook, ExcelReportContext context)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        var sourceChapters = context.OrderedPairs
            .Where(pair => pair.Source is not null)
            .Select(pair => pair.Source!)
            .Distinct()
            .Count();

        var targetChapters = context.OrderedPairs
            .Where(pair => pair.Target is not null)
            .Select(pair => pair.Target!)
            .Distinct()
            .Count();

        sheet.Cell(2, 1).Value = context.SourceFileName;
        sheet.Cell(2, 2).Value = context.TargetFileName;
        sheet.Cell(2, 3).Value = sourceChapters;
        sheet.Cell(2, 4).Value = targetChapters;
        sheet.Cell(2, 5).Value = context.OrderedPairs.Count(pair => pair.Source is not null && pair.Target is not null);
        sheet.Cell(2, 6).Value = context.OrderedPairs.Count(pair => pair.Source is not null && pair.Target is null);
        sheet.Cell(2, 7).Value = context.OrderedPairs.Count(pair => pair.Source is null && pair.Target is not null);
        sheet.Cell(2, 8).Value = context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Modified);
        sheet.Cell(2, 9).Value = context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Added);
        sheet.Cell(2, 10).Value = context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Deleted);
        sheet.Cell(2, 11).Value = context.ProcessingTime.ToString(@"mm\:ss\.fff");

        sheet.Columns(1, 2).Style.Alignment.WrapText = true;
        sheet.Columns().AdjustToContents();
        ExcelReportSheetFormatter.CapColumnWidth(sheet.Column(1), 60);
        ExcelReportSheetFormatter.CapColumnWidth(sheet.Column(2), 60);
        ExcelReportSheetFormatter.PrepareSheet(sheet, filter: false);
    }
}
