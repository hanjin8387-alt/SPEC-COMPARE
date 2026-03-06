using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;
using Spectre.Console;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecCompareApplication
{
    private const int SuccessExitCode = 0;
    private const double DefaultDiffThreshold = 0.85d;
    private const double DefaultChapterMatchThreshold = 0.70d;

    private readonly IAnsiConsole _console;

    public SpecCompareApplication(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public int Run(SpecCompareRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestValidation = ValidateAndResolveRequest(request);
        if (!requestValidation.IsValid || requestValidation.Value is null)
        {
            WriteValidationError(requestValidation.ErrorMessage);
            return requestValidation.ExitCode;
        }

        var resolvedRequest = requestValidation.Value;

        try
        {
            var runResult = ExecutePipeline(resolvedRequest, cancellationToken);

            _console.MarkupLine($"\n[green]Report saved:[/] {Markup.Escape(runResult.OutputPath)}");
            _console.MarkupLine($"[dim]Processed in {runResult.ProcessingTime:mm\\:ss\\.fff}[/]");

            foreach (var phase in runResult.PhaseTimings)
            {
                _console.MarkupLine($"[dim]- {Markup.Escape(phase.Name)}: {phase.Duration:mm\\:ss\\.fff}[/]");
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException operationCanceledException)
        {
            var sanitized = ExceptionSanitizer.Sanitize(operationCanceledException);
            _console.MarkupLine($"[yellow]Canceled:[/] {Markup.Escape(sanitized.Message)}");
            return ExceptionSanitizer.RuntimeExitCode;
        }
        catch (Exception exception)
        {
            var sanitized = ExceptionSanitizer.Sanitize(exception);
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(sanitized.Message)}");
            return sanitized.ExitCode;
        }
    }

    private ResolvedRequestValidationResult ValidateAndResolveRequest(SpecCompareRequest request)
    {
        var sourceValidation = InputValidator.ValidatePdfPath(request.SourcePdfPath, "Source PDF");
        if (!sourceValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(sourceValidation.ErrorMessage, sourceValidation.IsIoRelated);
        }

        var targetValidation = InputValidator.ValidatePdfPath(request.TargetPdfPath, "Target PDF");
        if (!targetValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(targetValidation.ErrorMessage, targetValidation.IsIoRelated);
        }

        var configValidation = InputValidator.ValidateOptionalConfigPath(request.ConfigPath);
        if (!configValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(configValidation.ErrorMessage, configValidation.IsIoRelated);
        }

        var outputValidation = InputValidator.ValidateOutputPath(request.OutputPath);
        if (!outputValidation.IsValid || string.IsNullOrWhiteSpace(outputValidation.ResolvedPath))
        {
            return ResolvedRequestValidationResult.Invalid(outputValidation.ErrorMessage, outputValidation.IsIoRelated);
        }

        var configLoadResult = PipelineConfigResolver.Load(request.ConfigPath);
        if (!configLoadResult.IsValid || configLoadResult.Config is null)
        {
            return ResolvedRequestValidationResult.Invalid(configLoadResult.ErrorMessage, configLoadResult.IsIoRelated);
        }

        var resolvedOptions = PipelineConfigResolver.Resolve(
            configLoadResult.Config,
            request.DiffThresholdOverride,
            request.ChapterMatchThresholdOverride,
            DefaultDiffThreshold,
            DefaultChapterMatchThreshold);

        var diffThresholdValidation = InputValidator.ValidateSimilarityThreshold(resolvedOptions.DiffThreshold, "Diff threshold");
        if (!diffThresholdValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(diffThresholdValidation.ErrorMessage);
        }

        var chapterMatchThresholdValidation = InputValidator.ValidateSimilarityThreshold(
            resolvedOptions.ChapterMatchThreshold,
            "Chapter match threshold");
        if (!chapterMatchThresholdValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(chapterMatchThresholdValidation.ErrorMessage);
        }

        var normalizationValidation = InputValidator.ValidateTextNormalizationOptions(resolvedOptions.TextNormalization);
        if (!normalizationValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(normalizationValidation.ErrorMessage);
        }

        var segmentationValidation = InputValidator.ValidateChapterSegmentationOptions(resolvedOptions.ChapterSegmentation);
        if (!segmentationValidation.IsValid)
        {
            return ResolvedRequestValidationResult.Invalid(segmentationValidation.ErrorMessage);
        }

        return ResolvedRequestValidationResult.Valid(
            new ResolvedSpecCompareRequest(
                request.SourcePdfPath,
                request.TargetPdfPath,
                outputValidation.ResolvedPath,
                request.ConfigPath,
                resolvedOptions));
    }

    private ComparisonRunResult ExecutePipeline(ResolvedSpecCompareRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfigPath))
        {
            _console.MarkupLine($"[dim]Config: {Markup.Escape(Path.GetFullPath(request.ConfigPath))}[/]");
        }

        _console.MarkupLine(
            $"[dim]Diff threshold: {request.Options.DiffThreshold:0.###} | Chapter match threshold: {request.Options.ChapterMatchThreshold:0.###}[/]");

        var runWatch = Stopwatch.StartNew();
        var phaseTimings = new List<PhaseTiming>();
        Stream? sourceStream = null;
        Stream? targetStream = null;

        try
        {
            List<PageText> sourcePages = new();
            List<PageText> targetPages = new();
            List<ChapterNode> sourceChapters = new();
            List<ChapterNode> targetChapters = new();
            ChapterMatchResult? chapterMatchResult = null;
            List<DiffItem> diffs = new();

            _console.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn()
                })
                .Start(progressContext =>
                {
                    var phase1 = progressContext.AddTask("[bold][[1/7]][/] Opening PDF inputs");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase1Watch = Stopwatch.StartNew();
                    sourceStream = PdfInputLoader.OpenRead(request.SourcePdfPath, cancellationToken);
                    targetStream = PdfInputLoader.OpenRead(request.TargetPdfPath, cancellationToken);
                    phase1Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Opening PDF inputs", phase1Watch.Elapsed));
                    phase1.Value = 100;

                    var phase2 = progressContext.AddTask("[bold][[2/7]][/] Extracting page text");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase2Watch = Stopwatch.StartNew();
                    sourcePages = TextExtractor.ExtractPages(sourceStream, cancellationToken);
                    targetPages = TextExtractor.ExtractPages(targetStream, cancellationToken);
                    sourceStream.Dispose();
                    sourceStream = null;
                    targetStream.Dispose();
                    targetStream = null;
                    phase2Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Extracting page text", phase2Watch.Elapsed));
                    phase2.Value = 100;

                    var phase3 = progressContext.AddTask("[bold][[3/7]][/] Cleaning and normalizing text");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase3Watch = Stopwatch.StartNew();
                    sourcePages = TextNormalizer.RemoveHeadersFooters(sourcePages, request.Options.TextNormalization, cancellationToken);
                    targetPages = TextNormalizer.RemoveHeadersFooters(targetPages, request.Options.TextNormalization, cancellationToken);
                    phase3Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Cleaning and normalizing text", phase3Watch.Elapsed));
                    phase3.Value = 100;

                    var phase4 = progressContext.AddTask("[bold][[4/7]][/] Segmenting chapters");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase4Watch = Stopwatch.StartNew();
                    sourceChapters = ChapterSegmenter.Segment(sourcePages, request.Options.ChapterSegmentation, cancellationToken);
                    targetChapters = ChapterSegmenter.Segment(targetPages, request.Options.ChapterSegmentation, cancellationToken);
                    phase4Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Segmenting chapters", phase4Watch.Elapsed));
                    phase4.Value = 100;

                    var phase5 = progressContext.AddTask("[bold][[5/7]][/] Matching chapters");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase5Watch = Stopwatch.StartNew();
                    chapterMatchResult = ChapterMatcher.Match(
                        sourceChapters,
                        targetChapters,
                        request.Options.ChapterMatchThreshold,
                        cancellationToken);
                    phase5Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Matching chapters", phase5Watch.Elapsed));
                    phase5.Value = 100;

                    var phase6 = progressContext.AddTask("[bold][[6/7]][/] Computing diffs");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase6Watch = Stopwatch.StartNew();
                    diffs = DiffEngine.ComputeDiffs(chapterMatchResult!, request.Options.DiffThreshold, cancellationToken);
                    phase6Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Computing diffs", phase6Watch.Elapsed));
                    phase6.Value = 100;

                    var phase7 = progressContext.AddTask("[bold][[7/7]][/] Generating Excel report");
                    cancellationToken.ThrowIfCancellationRequested();
                    var phase7Watch = Stopwatch.StartNew();
                    ExcelReporter.Generate(
                        request.OutputPath,
                        Path.GetFileName(request.SourcePdfPath),
                        Path.GetFileName(request.TargetPdfPath),
                        chapterMatchResult!.AllPairs,
                        diffs,
                        () => runWatch.Elapsed,
                        BuildDiagnostics(request, phaseTimings),
                        cancellationToken);
                    phase7Watch.Stop();
                    phaseTimings.Add(new PhaseTiming("Generating Excel report", phase7Watch.Elapsed));
                    phase7.Value = 100;
                });

            runWatch.Stop();

            return new ComparisonRunResult(request.OutputPath, runWatch.Elapsed, phaseTimings);
        }
        finally
        {
            if (runWatch.IsRunning)
            {
                runWatch.Stop();
            }

            sourceStream?.Dispose();
            targetStream?.Dispose();
        }
    }

    private void WriteValidationError(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "Invalid input." : message;
        _console.MarkupLine($"[red]Validation error:[/] {Markup.Escape(text)}");
    }

    private static IReadOnlyList<KeyValuePair<string, string>> BuildDiagnostics(
        ResolvedSpecCompareRequest request,
        IReadOnlyList<PhaseTiming> phaseTimings)
    {
        var diagnostics = new List<KeyValuePair<string, string>>
        {
            new("Output Path", request.OutputPath),
            new("Config Path", request.ConfigPath ?? "(none)"),
            new("Diff Threshold", request.Options.DiffThreshold.ToString("0.###")),
            new("Chapter Match Threshold", request.Options.ChapterMatchThreshold.ToString("0.###")),
            new("TextNormalization.HeaderFooterBandPercent", request.Options.TextNormalization.HeaderFooterBandPercent.ToString("0.###")),
            new("TextNormalization.MinRepeatingPages", request.Options.TextNormalization.MinRepeatingPages.ToString()),
            new("TextNormalization.RepeatingSimilarityThreshold", request.Options.TextNormalization.RepeatingSimilarityThreshold.ToString("0.###")),
            new("TextNormalization.LineMergeTolerance", request.Options.TextNormalization.LineMergeTolerance.ToString("0.###")),
            new("TextNormalization.ZoneLineLimit", request.Options.TextNormalization.ZoneLineLimit.ToString()),
            new("TextNormalization.SearchWindow", request.Options.TextNormalization.SearchWindow.ToString()),
            new("TextNormalization.MinZoneTextLength", request.Options.TextNormalization.MinZoneTextLength.ToString()),
            new("ChapterSegmentation.TocScanPageCount", request.Options.ChapterSegmentation.TocScanPageCount.ToString()),
            new("ChapterSegmentation.LayoutHeadingFontRatio", request.Options.ChapterSegmentation.LayoutHeadingFontRatio.ToString("0.###")),
            new("ChapterSegmentation.MinHeadingScore", request.Options.ChapterSegmentation.MinHeadingScore.ToString("0.###")),
            new("ChapterSegmentation.MaxHeadingWords", request.Options.ChapterSegmentation.MaxHeadingWords.ToString()),
            new("ChapterSegmentation.MaxHeadingLength", request.Options.ChapterSegmentation.MaxHeadingLength.ToString())
        };

        foreach (var phaseTiming in phaseTimings)
        {
            diagnostics.Add(new KeyValuePair<string, string>($"Phase.{phaseTiming.Name}", phaseTiming.Duration.ToString(@"mm\:ss\.fff")));
        }

        return diagnostics;
    }

    private sealed record ResolvedSpecCompareRequest(
        string SourcePdfPath,
        string TargetPdfPath,
        string OutputPath,
        string? ConfigPath,
        ResolvedPipelineOptions Options);

    private sealed record ComparisonRunResult(
        string OutputPath,
        TimeSpan ProcessingTime,
        IReadOnlyList<PhaseTiming> PhaseTimings);

    private readonly record struct PhaseTiming(string Name, TimeSpan Duration);

    private readonly record struct ResolvedRequestValidationResult(
        bool IsValid,
        ResolvedSpecCompareRequest? Value,
        string? ErrorMessage,
        int ExitCode)
    {
        public static ResolvedRequestValidationResult Valid(ResolvedSpecCompareRequest value)
        {
            return new ResolvedRequestValidationResult(true, value, null, SuccessExitCode);
        }

        public static ResolvedRequestValidationResult Invalid(string? errorMessage, bool isIoRelated = false)
        {
            return new ResolvedRequestValidationResult(
                false,
                null,
                errorMessage,
                isIoRelated ? ExceptionSanitizer.IoExitCode : ExceptionSanitizer.ValidationExitCode);
        }
    }
}
