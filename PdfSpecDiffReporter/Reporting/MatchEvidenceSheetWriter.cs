using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class MatchEvidenceSheetWriter
{
    private static readonly string[] Headers =
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

    public void Write(XLWorkbook workbook, ExcelReportContext context, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("MatchEvidence");
        ExcelReportSheetFormatter.WriteHeaders(sheet, Headers);

        var matchedPairs = context.OrderedPairs
            .Where(pair => pair.Source is not null && pair.Target is not null)
            .ToArray();

        for (var index = 0; index < matchedPairs.Length; index++)
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
            sheet.Cell(rowNumber, 6).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.OverallScore);
            sheet.Cell(rowNumber, 7).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.KeyScore);
            sheet.Cell(rowNumber, 8).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.TitleScore);
            sheet.Cell(rowNumber, 9).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.LevelScore);
            sheet.Cell(rowNumber, 10).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.OrderScore);
            sheet.Cell(rowNumber, 11).Value = ExcelReportSheetFormatter.NormalizeSimilarity(evidence.ContextScore);
            sheet.Cell(rowNumber, 12).Value = string.Join("; ", evidence.Reasons);
        }

        sheet.Columns(6, 11).Style.NumberFormat.Format = "0.0";
        sheet.Columns().AdjustToContents();
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(2), 45);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(4), 45);
        ExcelReportSheetFormatter.ApplyWrappedColumn(sheet.Column(12), 70);
        ExcelReportSheetFormatter.PrepareSheet(sheet);
    }
}
