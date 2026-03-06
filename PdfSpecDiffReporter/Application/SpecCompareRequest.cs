using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Application;

public sealed record SpecCompareRequest(
    string SourcePdfPath,
    string TargetPdfPath,
    string OutputPath,
    string? ConfigPath,
    double? DiffThresholdOverride,
    double? ChapterMatchThresholdOverride,
    bool? IncludeFullTextSheetOverride = null,
    int? PreviewTextLengthOverride = null,
    DiagnosticsVerbosity? DiagnosticsVerbosityOverride = null);
