namespace PdfSpecDiffReporter.Models;

public enum DiagnosticsVerbosity
{
    Minimal,
    Detailed
}

public sealed class ReportOptions
{
    public bool IncludeFullTextSheet { get; init; } = true;

    public int PreviewTextLength { get; init; } = 500;

    public DiagnosticsVerbosity DiagnosticsVerbosity { get; init; } = DiagnosticsVerbosity.Detailed;
}
