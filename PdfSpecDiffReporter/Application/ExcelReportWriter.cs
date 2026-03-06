using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Application;

public sealed class ExcelReportWriter : IReportWriter
{
    public void Write(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        Func<TimeSpan> processingTimeProvider,
        IReadOnlyList<KeyValuePair<string, string>> diagnostics,
        ReportOptions reportOptions,
        CancellationToken cancellationToken)
    {
        ExcelReporter.Generate(
            outputPath,
            sourceFileName,
            targetFileName,
            allPairs,
            diffs,
            processingTimeProvider,
            diagnostics,
            reportOptions,
            cancellationToken);
    }
}
