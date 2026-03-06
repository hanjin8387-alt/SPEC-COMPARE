using System.Diagnostics;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecComparePipeline
{
    private readonly IDocumentPipeline _documentPipeline;
    private readonly IReportWriter _reportWriter;
    private readonly SpecCompareDiagnosticsBuilder _diagnosticsBuilder;

    public SpecComparePipeline(
        IDocumentPipeline documentPipeline,
        IReportWriter reportWriter,
        SpecCompareDiagnosticsBuilder diagnosticsBuilder)
    {
        _documentPipeline = documentPipeline ?? throw new ArgumentNullException(nameof(documentPipeline));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        _diagnosticsBuilder = diagnosticsBuilder ?? throw new ArgumentNullException(nameof(diagnosticsBuilder));
    }

    public ComparisonRunResult Execute(
        ResolvedSpecCompareRequest request,
        ExecutionProgress progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        var runWatch = Stopwatch.StartNew();
        var phaseTimings = new List<PhaseTiming>();

        var sourceTask = _documentPipeline.ProcessAsync(
            ExecutionProgress.SourceTaskId,
            "Source",
            request.SourcePdfPath,
            request.Options,
            progress,
            cancellationToken);
        var targetTask = _documentPipeline.ProcessAsync(
            ExecutionProgress.TargetTaskId,
            "Target",
            request.TargetPdfPath,
            request.Options,
            progress,
            cancellationToken);

        Task.WhenAll(sourceTask, targetTask).GetAwaiter().GetResult();

        var source = sourceTask.GetAwaiter().GetResult();
        var target = targetTask.GetAwaiter().GetResult();
        phaseTimings.AddRange(source.PhaseTimings);
        phaseTimings.AddRange(target.PhaseTimings);

        progress.Update(ExecutionProgress.MatchTaskId, "[bold][[3/5]][/] Matching chapters", 10d);
        var matchWatch = Stopwatch.StartNew();
        var chapterMatchResult = ChapterMatcher.Match(
            source.Chapters,
            target.Chapters,
            request.Options.ChapterMatchThreshold,
            cancellationToken);
        matchWatch.Stop();
        phaseTimings.Add(new PhaseTiming("Matching chapters", matchWatch.Elapsed));
        progress.Update(ExecutionProgress.MatchTaskId, "[bold][[3/5]][/] Matching chapters", 100d);

        progress.Update(ExecutionProgress.DiffTaskId, "[bold][[4/5]][/] Computing diffs", 10d);
        var diffWatch = Stopwatch.StartNew();
        var diffs = DiffEngine.ComputeDiffs(chapterMatchResult, request.Options.DiffThreshold, cancellationToken);
        diffWatch.Stop();
        phaseTimings.Add(new PhaseTiming("Computing diffs", diffWatch.Elapsed));
        progress.Update(ExecutionProgress.DiffTaskId, "[bold][[4/5]][/] Computing diffs", 100d);

        progress.Update(ExecutionProgress.ReportTaskId, "[bold][[5/5]][/] Generating Excel report", 15d);
        var diagnostics = _diagnosticsBuilder.Build(request, phaseTimings);
        var reportWatch = Stopwatch.StartNew();
        _reportWriter.Write(
            request.OutputPath,
            source.FileName,
            target.FileName,
            chapterMatchResult.AllPairs,
            diffs,
            () => runWatch.Elapsed,
            diagnostics,
            request.Options.Reporting,
            cancellationToken);
        reportWatch.Stop();
        phaseTimings.Add(new PhaseTiming("Generating Excel report", reportWatch.Elapsed));
        progress.Update(ExecutionProgress.ReportTaskId, "[bold][[5/5]][/] Generating Excel report", 100d);

        runWatch.Stop();
        return new ComparisonRunResult(request.OutputPath, runWatch.Elapsed, phaseTimings);
    }
}
