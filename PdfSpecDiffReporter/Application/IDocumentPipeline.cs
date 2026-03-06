using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Application;

public interface IDocumentPipeline
{
    Task<ProcessedDocument> ProcessAsync(
        string taskId,
        string taskLabel,
        string pdfPath,
        ResolvedPipelineOptions options,
        ExecutionProgress progress,
        CancellationToken cancellationToken);
}
