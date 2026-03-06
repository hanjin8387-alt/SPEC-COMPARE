namespace PdfSpecDiffReporter.Application;

public sealed class ExecutionProgress
{
    public const string SourceTaskId = "source";
    public const string TargetTaskId = "target";
    public const string MatchTaskId = "match";
    public const string DiffTaskId = "diff";
    public const string ReportTaskId = "report";

    private readonly object _sync = new();
    private readonly Dictionary<string, ProgressTaskStatus> _tasks = new(StringComparer.Ordinal)
    {
        [SourceTaskId] = new("[bold][[1/5]][/] Source document", 0d),
        [TargetTaskId] = new("[bold][[2/5]][/] Target document", 0d),
        [MatchTaskId] = new("[bold][[3/5]][/] Matching chapters", 0d),
        [DiffTaskId] = new("[bold][[4/5]][/] Computing diffs", 0d),
        [ReportTaskId] = new("[bold][[5/5]][/] Generating Excel report", 0d)
    };

    public void Update(string taskId, string description, double value)
    {
        lock (_sync)
        {
            _tasks[taskId] = new ProgressTaskStatus(description, Math.Clamp(value, 0d, 100d));
        }
    }

    public IReadOnlyDictionary<string, ProgressTaskStatus> Snapshot()
    {
        lock (_sync)
        {
            return _tasks.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }
    }
}

public readonly record struct ProgressTaskStatus(string Description, double Value);
