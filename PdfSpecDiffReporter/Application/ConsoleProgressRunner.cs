using System.Runtime.ExceptionServices;
using Spectre.Console;

namespace PdfSpecDiffReporter.Application;

public sealed class ConsoleProgressRunner
{
    private readonly IAnsiConsole _console;

    public ConsoleProgressRunner(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public T Run<T>(Func<ExecutionProgress, T> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var progress = new ExecutionProgress();
        T? result = default;
        Exception? failure = null;

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
            .Start(context =>
            {
                var tasks = new Dictionary<string, ProgressTask>(StringComparer.Ordinal)
                {
                    [ExecutionProgress.SourceTaskId] = context.AddTask("[bold][[1/5]][/] Source document"),
                    [ExecutionProgress.TargetTaskId] = context.AddTask("[bold][[2/5]][/] Target document"),
                    [ExecutionProgress.MatchTaskId] = context.AddTask("[bold][[3/5]][/] Matching chapters"),
                    [ExecutionProgress.DiffTaskId] = context.AddTask("[bold][[4/5]][/] Computing diffs"),
                    [ExecutionProgress.ReportTaskId] = context.AddTask("[bold][[5/5]][/] Generating Excel report")
                };

                var operationTask = Task.Run(() => operation(progress), cancellationToken);

                try
                {
                    while (!operationTask.Wait(100))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Apply(tasks, progress.Snapshot());
                    }

                    Apply(tasks, progress.Snapshot());
                    result = operationTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    failure = ex is AggregateException aggregateException
                        ? aggregateException.Flatten().InnerExceptions.Count == 1
                            ? aggregateException.InnerException
                            : aggregateException
                        : ex;
                }
            });

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    private static void Apply(
        IReadOnlyDictionary<string, ProgressTask> tasks,
        IReadOnlyDictionary<string, ProgressTaskStatus> snapshot)
    {
        foreach (var entry in snapshot)
        {
            if (!tasks.TryGetValue(entry.Key, out var task))
            {
                continue;
            }

            task.Description = entry.Value.Description;
            task.Value = entry.Value.Value;
        }
    }
}
