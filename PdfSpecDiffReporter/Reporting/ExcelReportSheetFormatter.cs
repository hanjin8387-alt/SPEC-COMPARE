using ClosedXML.Excel;

namespace PdfSpecDiffReporter.Reporting;

internal static class ExcelReportSheetFormatter
{
    public static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
        }
    }

    public static void PrepareSheet(IXLWorksheet sheet, bool filter = true)
    {
        sheet.SheetView.FreezeRows(1);
        if (filter && sheet.LastRowUsed() is not null && sheet.LastColumnUsed() is not null)
        {
            sheet.Range(1, 1, sheet.LastRowUsed()!.RowNumber(), sheet.LastColumnUsed()!.ColumnNumber()).SetAutoFilter();
        }
    }

    public static void ApplyWrappedColumn(IXLColumn column, double maxWidth)
    {
        column.Style.Alignment.WrapText = true;
        column.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        CapColumnWidth(column, maxWidth);
    }

    public static void CapColumnWidth(IXLColumn column, double maxWidth)
    {
        if (column.Width > maxWidth)
        {
            column.Width = maxWidth;
        }
    }

    public static double NormalizeSimilarity(double score)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return 0d;
        }

        var percentage = Math.Round(score * 100d, 1, MidpointRounding.AwayFromZero);
        return Math.Clamp(percentage, 0d, 100d);
    }

    public static string CreatePreview(string? value, int previewTextLength)
    {
        if (string.IsNullOrEmpty(value) || previewTextLength <= 0)
        {
            return string.Empty;
        }

        return value.Length <= previewTextLength
            ? value
            : $"{value[..previewTextLength]}...";
    }
}
