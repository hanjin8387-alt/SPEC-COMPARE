using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Application;

public sealed record ResolvedSpecCompareRequest(
    string SourcePdfPath,
    string TargetPdfPath,
    string OutputPath,
    string? ConfigPath,
    ResolvedPipelineOptions Options);
