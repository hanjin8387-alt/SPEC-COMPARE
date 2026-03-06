using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Reporting;

internal sealed class ExcelReportContext
{
    private ExcelReportContext(
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> orderedPairs,
        IReadOnlyList<DiffItem> orderedDiffs,
        IReadOnlyDictionary<string, string> titleByKey,
        TimeSpan processingTime,
        IReadOnlyList<KeyValuePair<string, string>> diagnostics,
        ReportOptions reportOptions)
    {
        SourceFileName = sourceFileName;
        TargetFileName = targetFileName;
        OrderedPairs = orderedPairs;
        OrderedDiffs = orderedDiffs;
        TitleByKey = titleByKey;
        ProcessingTime = processingTime;
        Diagnostics = diagnostics;
        ReportOptions = reportOptions;
    }

    public string SourceFileName { get; }

    public string TargetFileName { get; }

    public IReadOnlyList<ChapterPair> OrderedPairs { get; }

    public IReadOnlyList<DiffItem> OrderedDiffs { get; }

    public IReadOnlyDictionary<string, string> TitleByKey { get; }

    public TimeSpan ProcessingTime { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Diagnostics { get; }

    public ReportOptions ReportOptions { get; }

    public static ExcelReportContext Create(
        string sourceFileName,
        string targetFileName,
        IReadOnlyList<ChapterPair> allPairs,
        IReadOnlyList<DiffItem> diffs,
        TimeSpan processingTime,
        IReadOnlyList<KeyValuePair<string, string>>? diagnostics,
        ReportOptions? reportOptions)
    {
        var orderedPairs = allPairs
            .OrderBy(pair => pair.Source?.Order ?? int.MaxValue)
            .ThenBy(pair => pair.Target?.Order ?? int.MaxValue)
            .ThenBy(pair => pair.Source?.Key ?? pair.Target?.Key ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var titleByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in orderedPairs)
        {
            if (pair.Source is not null &&
                !string.IsNullOrWhiteSpace(pair.Source.Key) &&
                !titleByKey.ContainsKey(pair.Source.Key))
            {
                titleByKey[pair.Source.Key] = pair.Source.Title ?? string.Empty;
            }

            if (pair.Target is not null &&
                !string.IsNullOrWhiteSpace(pair.Target.Key) &&
                !titleByKey.ContainsKey(pair.Target.Key))
            {
                titleByKey[pair.Target.Key] = pair.Target.Title ?? string.Empty;
            }
        }

        return new ExcelReportContext(
            sourceFileName ?? string.Empty,
            targetFileName ?? string.Empty,
            orderedPairs,
            diffs.ToArray(),
            titleByKey,
            processingTime,
            diagnostics ?? Array.Empty<KeyValuePair<string, string>>(),
            reportOptions ?? new ReportOptions());
    }
}
