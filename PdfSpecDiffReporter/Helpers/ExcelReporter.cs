using ClosedXML.Excel;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Reporting;

namespace PdfSpecDiffReporter.Helpers;

public static class ExcelReporter
{
    public static void Generate(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        Func<TimeSpan> processingTimeProvider,
        IReadOnlyList<KeyValuePair<string, string>>? diagnostics = null,
        ReportOptions? reportOptions = null,
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

            var context = ExcelReportContext.Create(
                sourceFileName,
                targetFileName,
                allPairs,
                diffs,
                processingTimeProvider(),
                diagnostics,
                reportOptions);

            using var workbook = new XLWorkbook();
            new SummarySheetWriter().Write(workbook, context);
            new ChangeDetailsSheetWriter().Write(workbook, context, cancellationToken);

            if (context.ReportOptions.IncludeFullTextSheet)
            {
                new FullTextSheetWriter().Write(workbook, context, cancellationToken);
            }

            new MatchEvidenceSheetWriter().Write(workbook, context, cancellationToken);
            new UnmatchedSheetWriter().Write(workbook, context, cancellationToken);
            new DiagnosticsSheetWriter().Write(workbook, context);

            using var fileStream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.SaveAs(fileStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }
}
