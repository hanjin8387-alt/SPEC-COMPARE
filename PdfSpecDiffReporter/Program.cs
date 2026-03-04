using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Text.Json;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;
using Spectre.Console;

const int SuccessExitCode = 0;
const double DefaultDiffThreshold = 0.85d;
const double DefaultChapterMatchThreshold = 0.70d;

var sourcePdfArgument = new Argument<string>(
    name: "source_pdf",
    description: "Path to the source PDF.");

var targetPdfArgument = new Argument<string>(
    name: "target_pdf",
    description: "Path to the target PDF.");

var outputOption = new Option<string>(
    aliases: new[] { "--output", "-o" },
    getDefaultValue: () => @".\diff_report.xlsx",
    description: "Path to the generated diff report (.xlsx).");

var configOption = new Option<string?>(
    aliases: new[] { "--config", "-c" },
    description: "Optional JSON config file path.");

var diffThresholdOption = new Option<double?>(
    name: "--diff-threshold",
    description: $"Similarity threshold for diff classification (0 < value <= 1, default {DefaultDiffThreshold:0.##}).");

var chapterMatchThresholdOption = new Option<double?>(
    name: "--chapter-match-threshold",
    description: $"Similarity threshold for chapter matching (0 < value <= 1, default {DefaultChapterMatchThreshold:0.##}).");

var rootCommand = new RootCommand("PdfSpecDiffReporter - PDF specification diff tool.");
rootCommand.AddArgument(sourcePdfArgument);
rootCommand.AddArgument(targetPdfArgument);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(configOption);
rootCommand.AddOption(diffThresholdOption);
rootCommand.AddOption(chapterMatchThresholdOption);

rootCommand.SetHandler(
    (InvocationContext context) =>
    {
        var sourcePdfPath = context.ParseResult.GetValueForArgument(sourcePdfArgument);
        var targetPdfPath = context.ParseResult.GetValueForArgument(targetPdfArgument);
        var outputPath = context.ParseResult.GetValueForOption(outputOption) ?? @".\diff_report.xlsx";
        var configPath = context.ParseResult.GetValueForOption(configOption);
        var diffThresholdOverride = context.ParseResult.GetValueForOption(diffThresholdOption);
        var chapterMatchThresholdOverride = context.ParseResult.GetValueForOption(chapterMatchThresholdOption);

        context.ExitCode = RunPipeline(
            sourcePdfPath,
            targetPdfPath,
            outputPath,
            configPath,
            diffThresholdOverride,
            chapterMatchThresholdOverride);
    });

return await rootCommand.InvokeAsync(args);

int RunPipeline(
    string sourcePdfPath,
    string targetPdfPath,
    string outputPath,
    string? configPath,
    double? diffThresholdOverride,
    double? chapterMatchThresholdOverride)
{
    var sourceValidation = InputValidator.ValidatePdfPath(sourcePdfPath, "Source PDF");
    if (!sourceValidation.IsValid)
    {
        WriteValidationError(sourceValidation.ErrorMessage);
        return sourceValidation.IsIoRelated
            ? ExceptionSanitizer.IoExitCode
            : ExceptionSanitizer.ValidationExitCode;
    }

    var targetValidation = InputValidator.ValidatePdfPath(targetPdfPath, "Target PDF");
    if (!targetValidation.IsValid)
    {
        WriteValidationError(targetValidation.ErrorMessage);
        return targetValidation.IsIoRelated
            ? ExceptionSanitizer.IoExitCode
            : ExceptionSanitizer.ValidationExitCode;
    }

    var configValidation = InputValidator.ValidateOptionalConfigPath(configPath);
    if (!configValidation.IsValid)
    {
        WriteValidationError(configValidation.ErrorMessage);
        return configValidation.IsIoRelated
            ? ExceptionSanitizer.IoExitCode
            : ExceptionSanitizer.ValidationExitCode;
    }

    var outputValidation = InputValidator.ValidateOutputPath(outputPath);
    if (!outputValidation.IsValid || string.IsNullOrWhiteSpace(outputValidation.ResolvedPath))
    {
        WriteValidationError(outputValidation.ErrorMessage);
        return outputValidation.IsIoRelated
            ? ExceptionSanitizer.IoExitCode
            : ExceptionSanitizer.ValidationExitCode;
    }

    var configLoadResult = LoadConfig(configPath);
    if (!configLoadResult.IsValid || configLoadResult.Config is null)
    {
        WriteValidationError(configLoadResult.ErrorMessage);
        return configLoadResult.IsIoRelated
            ? ExceptionSanitizer.IoExitCode
            : ExceptionSanitizer.ValidationExitCode;
    }

    var config = configLoadResult.Config;
    var textNormalizationOptions = config.TextNormalization ?? new TextNormalizationOptions();
    var chapterSegmentationOptions = config.ChapterSegmentation ?? new ChapterSegmentationOptions();
    var diffThreshold = diffThresholdOverride ?? config.DiffThreshold ?? DefaultDiffThreshold;
    var chapterMatchThreshold = chapterMatchThresholdOverride ?? config.ChapterMatchThreshold ?? DefaultChapterMatchThreshold;

    var diffThresholdValidation = InputValidator.ValidateSimilarityThreshold(diffThreshold, "Diff threshold");
    if (!diffThresholdValidation.IsValid)
    {
        WriteValidationError(diffThresholdValidation.ErrorMessage);
        return ExceptionSanitizer.ValidationExitCode;
    }

    var chapterMatchThresholdValidation = InputValidator.ValidateSimilarityThreshold(chapterMatchThreshold, "Chapter match threshold");
    if (!chapterMatchThresholdValidation.IsValid)
    {
        WriteValidationError(chapterMatchThresholdValidation.ErrorMessage);
        return ExceptionSanitizer.ValidationExitCode;
    }

    var normalizationValidation = InputValidator.ValidateTextNormalizationOptions(textNormalizationOptions);
    if (!normalizationValidation.IsValid)
    {
        WriteValidationError(normalizationValidation.ErrorMessage);
        return ExceptionSanitizer.ValidationExitCode;
    }

    var segmentationValidation = InputValidator.ValidateChapterSegmentationOptions(chapterSegmentationOptions);
    if (!segmentationValidation.IsValid)
    {
        WriteValidationError(segmentationValidation.ErrorMessage);
        return ExceptionSanitizer.ValidationExitCode;
    }

    if (!string.IsNullOrWhiteSpace(configPath))
    {
        AnsiConsole.MarkupLine($"[dim]Config: {Markup.Escape(Path.GetFullPath(configPath))}[/]");
    }

    AnsiConsole.MarkupLine($"[dim]Diff threshold: {diffThreshold:0.###} | Chapter match threshold: {chapterMatchThreshold:0.###}[/]");

    var resolvedOutputPath = outputValidation.ResolvedPath;
    var cancellationSource = new CancellationTokenSource();
    ConsoleCancelEventHandler cancellationHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    Console.CancelKeyPress += cancellationHandler;

    var runWatch = Stopwatch.StartNew();
    var phaseTimings = new List<(string Name, TimeSpan Duration)>();
    MemoryStream? sourceStream = null;
    MemoryStream? targetStream = null;

    try
    {
        List<PageText> sourcePages = new();
        List<PageText> targetPages = new();
        List<ChapterNode> sourceChapters = new();
        List<ChapterNode> targetChapters = new();
        ChapterMatchResult? chapterMatchResult = null;
        List<DiffItem> diffs = new();

        AnsiConsole.Progress()
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
                var phase1 = progressContext.AddTask("[bold][[1/7]][/] Secure ingestion");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase1Watch = Stopwatch.StartNew();
                sourceStream = SecureIngestion.LoadToMemory(sourcePdfPath);
                targetStream = SecureIngestion.LoadToMemory(targetPdfPath);
                phase1Watch.Stop();
                phaseTimings.Add(("Secure ingestion", phase1Watch.Elapsed));
                phase1.Value = 100;

                var phase2 = progressContext.AddTask("[bold][[2/7]][/] Extracting page text");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase2Watch = Stopwatch.StartNew();
                sourcePages = TextExtractor.ExtractPages(sourceStream);
                targetPages = TextExtractor.ExtractPages(targetStream);
                sourceStream.Dispose();
                sourceStream = null;
                targetStream.Dispose();
                targetStream = null;
                phase2Watch.Stop();
                phaseTimings.Add(("Extracting page text", phase2Watch.Elapsed));
                phase2.Value = 100;

                var phase3 = progressContext.AddTask("[bold][[3/7]][/] Cleaning and normalizing text");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase3Watch = Stopwatch.StartNew();
                sourcePages = TextNormalizer.RemoveHeadersFooters(sourcePages, textNormalizationOptions);
                targetPages = TextNormalizer.RemoveHeadersFooters(targetPages, textNormalizationOptions);
                phase3Watch.Stop();
                phaseTimings.Add(("Cleaning and normalizing text", phase3Watch.Elapsed));
                phase3.Value = 100;

                var phase4 = progressContext.AddTask("[bold][[4/7]][/] Segmenting chapters");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase4Watch = Stopwatch.StartNew();
                sourceChapters = ChapterSegmenter.Segment(sourcePages, chapterSegmentationOptions);
                targetChapters = ChapterSegmenter.Segment(targetPages, chapterSegmentationOptions);
                phase4Watch.Stop();
                phaseTimings.Add(("Segmenting chapters", phase4Watch.Elapsed));
                phase4.Value = 100;

                var phase5 = progressContext.AddTask("[bold][[5/7]][/] Matching chapters");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase5Watch = Stopwatch.StartNew();
                chapterMatchResult = ChapterMatcher.Match(sourceChapters, targetChapters, chapterMatchThreshold);
                phase5Watch.Stop();
                phaseTimings.Add(("Matching chapters", phase5Watch.Elapsed));
                phase5.Value = 100;

                var phase6 = progressContext.AddTask("[bold][[6/7]][/] Computing diffs");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase6Watch = Stopwatch.StartNew();
                diffs = DiffEngine.ComputeDiffs(chapterMatchResult!, diffThreshold);
                phase6Watch.Stop();
                phaseTimings.Add(("Computing diffs", phase6Watch.Elapsed));
                phase6.Value = 100;

                var phase7 = progressContext.AddTask("[bold][[7/7]][/] Generating Excel report");
                cancellationSource.Token.ThrowIfCancellationRequested();
                var phase7Watch = Stopwatch.StartNew();
                var allPairs = BuildAllPairs(chapterMatchResult!);
                ExcelReporter.Generate(
                    resolvedOutputPath,
                    Path.GetFileName(sourcePdfPath),
                    Path.GetFileName(targetPdfPath),
                    allPairs,
                    diffs,
                    () => runWatch.Elapsed);
                phase7Watch.Stop();
                phaseTimings.Add(("Generating Excel report", phase7Watch.Elapsed));
                phase7.Value = 100;
            });

        if (runWatch.IsRunning)
        {
            runWatch.Stop();
        }

        AnsiConsole.MarkupLine($"\n[green]Report saved:[/] {Markup.Escape(resolvedOutputPath)}");
        AnsiConsole.MarkupLine($"[dim]Processed in {runWatch.Elapsed:mm\\:ss\\.fff}[/]");

        foreach (var (name, duration) in phaseTimings)
        {
            AnsiConsole.MarkupLine($"[dim]- {Markup.Escape(name)}: {duration:mm\\:ss\\.fff}[/]");
        }

        return SuccessExitCode;
    }
    catch (OperationCanceledException operationCanceledException)
    {
        var sanitized = ExceptionSanitizer.Sanitize(operationCanceledException);
        AnsiConsole.MarkupLine($"[yellow]Canceled:[/] {Markup.Escape(sanitized.Message)}");
        return ExceptionSanitizer.RuntimeExitCode;
    }
    catch (Exception ex)
    {
        var sanitized = ExceptionSanitizer.Sanitize(ex);
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(sanitized.Message)}");
        return sanitized.ExitCode;
    }
    finally
    {
        Console.CancelKeyPress -= cancellationHandler;
        sourceStream?.Dispose();
        targetStream?.Dispose();
        cancellationSource.Dispose();
    }
}

static List<ChapterPair> BuildAllPairs(ChapterMatchResult chapterMatchResult)
{
    var allPairs = new List<ChapterPair>(chapterMatchResult.Matches);
    allPairs.AddRange(chapterMatchResult.UnmatchedSource.Select(node => new ChapterPair(node, null, 0d)));
    allPairs.AddRange(chapterMatchResult.UnmatchedTarget.Select(node => new ChapterPair(null, node, 0d)));
    return allPairs;
}

static ConfigLoadResult LoadConfig(string? configPath)
{
    if (string.IsNullOrWhiteSpace(configPath))
    {
        return ConfigLoadResult.Valid(new PipelineConfig());
    }

    try
    {
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<PipelineConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        return ConfigLoadResult.Valid(config ?? new PipelineConfig());
    }
    catch (UnauthorizedAccessException)
    {
        return ConfigLoadResult.Invalid("Config file cannot be read.", isIoRelated: true);
    }
    catch (IOException)
    {
        return ConfigLoadResult.Invalid("Config file cannot be read.", isIoRelated: true);
    }
    catch (JsonException)
    {
        return ConfigLoadResult.Invalid("Config JSON is invalid.");
    }
}

static void WriteValidationError(string? message)
{
    var text = string.IsNullOrWhiteSpace(message) ? "Invalid input." : message;
    AnsiConsole.MarkupLine($"[red]Validation error:[/] {Markup.Escape(text)}");
}

readonly record struct ConfigLoadResult(
    bool IsValid,
    PipelineConfig? Config,
    string? ErrorMessage,
    bool IsIoRelated)
{
    public static ConfigLoadResult Valid(PipelineConfig config)
    {
        return new ConfigLoadResult(true, config, null, false);
    }

    public static ConfigLoadResult Invalid(string errorMessage, bool isIoRelated = false)
    {
        return new ConfigLoadResult(false, null, errorMessage, isIoRelated);
    }
}
