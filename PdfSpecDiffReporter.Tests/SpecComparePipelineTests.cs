using PdfSpecDiffReporter.Application;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class SpecComparePipelineTests
{
    [Fact]
    public async Task Execute_ProcessesSourceAndTargetDocumentsConcurrently()
    {
        var documentPipeline = new BlockingDocumentPipeline();
        var reportWriter = new RecordingReportWriter();
        var pipeline = new SpecComparePipeline(
            documentPipeline,
            reportWriter,
            new SpecCompareDiagnosticsBuilder());
        var request = new ResolvedSpecCompareRequest(
            "source.pdf",
            "target.pdf",
            "report.xlsx",
            null,
            new ResolvedPipelineOptions(
                0.85d,
                0.70d,
                new TextNormalizationOptions(),
                new ChapterSegmentationOptions(),
                new ReportOptions
                {
                    IncludeFullTextSheet = false,
                    PreviewTextLength = 24,
                    DiagnosticsVerbosity = DiagnosticsVerbosity.Minimal
                }));

        var execution = Task.Run(() => pipeline.Execute(request, new ExecutionProgress(), CancellationToken.None));

        await documentPipeline.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(documentPipeline.MaxConcurrent >= 2);

        documentPipeline.Release();
        var result = await execution;

        Assert.Equal("report.xlsx", result.OutputPath);
        Assert.Equal(1, reportWriter.CallCount);
        Assert.False(reportWriter.LastReportOptions!.IncludeFullTextSheet);
        Assert.Contains(result.PhaseTimings, phase => phase.Name == "Matching chapters");
        Assert.Contains(result.PhaseTimings, phase => phase.Name == "Computing diffs");
    }

    private sealed class BlockingDocumentPipeline : IDocumentPipeline
    {
        private int _activeCount;
        private int _maxConcurrent;

        public TaskCompletionSource<bool> BothStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseGate { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxConcurrent => _maxConcurrent;

        public void Release()
        {
            ReleaseGate.TrySetResult(true);
        }

        public async Task<ProcessedDocument> ProcessAsync(
            string taskId,
            string taskLabel,
            string pdfPath,
            ResolvedPipelineOptions options,
            ExecutionProgress progress,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeCount);
            InterlockedExtensions.Max(ref _maxConcurrent, active);
            progress.Update(taskId, taskLabel, 50d);

            if (active >= 2)
            {
                BothStarted.TrySetResult(true);
            }

            await BothStarted.Task.WaitAsync(cancellationToken);
            await ReleaseGate.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _activeCount);

            var node = CreateNode("1", "Overview", "Shared content.");
            return new ProcessedDocument(
                Path.GetFileName(pdfPath),
                new[] { node },
                new[] { new PhaseTiming($"{taskLabel}: fake", TimeSpan.Zero) });
        }
    }

    private sealed class RecordingReportWriter : IReportWriter
    {
        public int CallCount { get; private set; }

        public ReportOptions? LastReportOptions { get; private set; }

        public void Write(
            string outputPath,
            string sourceFileName,
            string targetFileName,
            IReadOnlyList<ChapterPair> allPairs,
            IReadOnlyList<DiffItem> diffs,
            Func<TimeSpan> processingTimeProvider,
            IReadOnlyList<KeyValuePair<string, string>> diagnostics,
            ReportOptions reportOptions,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReportOptions = reportOptions;
        }
    }

    private static ChapterNode CreateNode(string key, string title, string content)
    {
        var lines = content
            .Split('\n')
            .Select((line, index) => new TextLine(
                1,
                line,
                TextNormalizer.Normalize(line),
                -index,
                0d,
                0d,
                0d,
                0d,
                0))
            .ToArray();
        var blocks = TextBlockBuilder.BuildBlocks(lines);

        return new ChapterNode
        {
            Key = key,
            MatchKey = key,
            Title = title,
            Level = 1,
            Blocks = blocks,
            PageStart = 1,
            PageEnd = 1,
            Order = 0
        };
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            while (true)
            {
                var snapshot = target;
                if (snapshot >= value)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref target, value, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }
}
