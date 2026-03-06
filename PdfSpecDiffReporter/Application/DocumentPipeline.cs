using System.Diagnostics;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Application;

public sealed class DocumentPipeline : IDocumentPipeline
{
    public Task<ProcessedDocument> ProcessAsync(
        string taskId,
        string taskLabel,
        string pdfPath,
        ResolvedPipelineOptions options,
        ExecutionProgress progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Process(taskId, taskLabel, pdfPath, options, progress, cancellationToken), cancellationToken);
    }

    private static ProcessedDocument Process(
        string taskId,
        string taskLabel,
        string pdfPath,
        ResolvedPipelineOptions options,
        ExecutionProgress progress,
        CancellationToken cancellationToken)
    {
        var phaseTimings = new List<PhaseTiming>();

        progress.Update(taskId, $"[bold]{taskLabel}[/] Opening PDF", 5d);
        var openWatch = Stopwatch.StartNew();
        using var stream = PdfInputLoader.OpenRead(pdfPath, cancellationToken);
        openWatch.Stop();
        phaseTimings.Add(new PhaseTiming($"{taskLabel}: Opening PDF", openWatch.Elapsed));

        progress.Update(taskId, $"[bold]{taskLabel}[/] Extracting lines", 35d);
        var extractWatch = Stopwatch.StartNew();
        var pages = TextExtractor.ExtractPages(stream, options.TextNormalization.LineMergeTolerance, cancellationToken);
        extractWatch.Stop();
        phaseTimings.Add(new PhaseTiming($"{taskLabel}: Extracting text", extractWatch.Elapsed));

        progress.Update(taskId, $"[bold]{taskLabel}[/] Normalizing text", 70d);
        var normalizeWatch = Stopwatch.StartNew();
        var normalizedPages = TextNormalizer.RemoveHeadersFooters(pages, options.TextNormalization, cancellationToken);
        normalizeWatch.Stop();
        phaseTimings.Add(new PhaseTiming($"{taskLabel}: Normalizing text", normalizeWatch.Elapsed));

        progress.Update(taskId, $"[bold]{taskLabel}[/] Segmenting chapters", 90d);
        var segmentWatch = Stopwatch.StartNew();
        var chapters = ChapterSegmenter.Segment(normalizedPages, options.ChapterSegmentation, cancellationToken);
        segmentWatch.Stop();
        phaseTimings.Add(new PhaseTiming($"{taskLabel}: Segmenting chapters", segmentWatch.Elapsed));

        progress.Update(taskId, $"[bold]{taskLabel}[/] Complete", 100d);
        return new ProcessedDocument(Path.GetFileName(pdfPath), chapters, phaseTimings);
    }
}

public sealed record ProcessedDocument(
    string FileName,
    IReadOnlyList<ChapterNode> Chapters,
    IReadOnlyList<PhaseTiming> PhaseTimings);
