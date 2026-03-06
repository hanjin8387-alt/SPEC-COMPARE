using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Application;

public interface IReportWriter
{
    void Write(
        string outputPath,
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        Func<TimeSpan> processingTimeProvider,
        IReadOnlyList<KeyValuePair<string, string>> diagnostics,
        ReportOptions reportOptions,
        CancellationToken cancellationToken);
}
