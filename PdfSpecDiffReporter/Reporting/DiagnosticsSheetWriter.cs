using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class DiagnosticsSheetWriter
{
    private static readonly string[] Headers =
    {
        "Key",
        "Value"
    };

    public void Write(XLWorkbook workbook, ExcelReportContext context)
    {
        var sheet = workbook.Worksheets.Add("Diagnostics");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        var rows = new List<KeyValuePair<string, string>>
        {
            new("Generated UTC", DateTime.UtcNow.ToString("u")),
            new("Source File", context.SourceFileName),
            new("Target File", context.TargetFileName),
            new("Matched Chapters", context.OrderedPairs.Count(pair => pair.Source is not null && pair.Target is not null).ToString()),
            new("Unmatched Source Chapters", context.OrderedPairs.Count(pair => pair.Source is not null && pair.Target is null).ToString()),
            new("Unmatched Target Chapters", context.OrderedPairs.Count(pair => pair.Source is null && pair.Target is not null).ToString()),
            new("Modified Items", context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Modified).ToString()),
            new("Added Items", context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Added).ToString()),
            new("Deleted Items", context.OrderedDiffs.Count(item => item.ChangeType == ChangeType.Deleted).ToString()),
            new("Processing Time", context.ProcessingTime.ToString(@"mm\:ss\.fff"))
        };

        rows.AddRange(context.Diagnostics);

        for (var index = 0; index < rows.Count; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].Key;
            sheet.Cell(index + 2, 2).Value = rows[index].Value;
        }

        sheet.Columns().AdjustToContents();
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(2), 80);
        ExcelReportSheetFormatter.PrepareSheet(sheet, filter: false);
    }
}
