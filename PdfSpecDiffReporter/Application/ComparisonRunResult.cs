namespace PdfSpecDiffReporter.Application;

public sealed record ComparisonRunResult(
    string OutputPath,
    TimeSpan ProcessingTime,
    IReadOnlyList<PhaseTiming> PhaseTimings);

public sealed record PhaseTiming(string Name, TimeSpan Duration);
