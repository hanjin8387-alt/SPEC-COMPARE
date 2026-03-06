using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecCompareDiagnosticsBuilder
{
    public IReadOnlyList<KeyValuePair<string, string>> Build(
        ResolvedSpecCompareRequest request,
        IReadOnlyList<PhaseTiming> phaseTimings)
    {
        var diagnostics = new List<KeyValuePair<string, string>>
        {
            new("Output Path", request.OutputPath),
            new("Config Path", request.ConfigPath ?? "(none)"),
            new("Diff Threshold", request.Options.DiffThreshold.ToString("0.###")),
            new("Chapter Match Threshold", request.Options.ChapterMatchThreshold.ToString("0.###"))
        };

        if (request.Options.Reporting.DiagnosticsVerbosity == DiagnosticsVerbosity.Detailed)
        {
            diagnostics.Add(new KeyValuePair<string, string>(
                "Reporting.IncludeFullTextSheet",
                request.Options.Reporting.IncludeFullTextSheet.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "Reporting.PreviewTextLength",
                request.Options.Reporting.PreviewTextLength.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "Reporting.DiagnosticsVerbosity",
                request.Options.Reporting.DiagnosticsVerbosity.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.HeaderFooterBandPercent",
                request.Options.TextNormalization.HeaderFooterBandPercent.ToString("0.###")));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.MinRepeatingPages",
                request.Options.TextNormalization.MinRepeatingPages.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.RepeatingSimilarityThreshold",
                request.Options.TextNormalization.RepeatingSimilarityThreshold.ToString("0.###")));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.LineMergeTolerance",
                request.Options.TextNormalization.LineMergeTolerance.ToString("0.###")));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.ZoneLineLimit",
                request.Options.TextNormalization.ZoneLineLimit.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.SearchWindow",
                request.Options.TextNormalization.SearchWindow.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "TextNormalization.MinZoneTextLength",
                request.Options.TextNormalization.MinZoneTextLength.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "ChapterSegmentation.TocScanPageCount",
                request.Options.ChapterSegmentation.TocScanPageCount.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "ChapterSegmentation.LayoutHeadingFontRatio",
                request.Options.ChapterSegmentation.LayoutHeadingFontRatio.ToString("0.###")));
            diagnostics.Add(new KeyValuePair<string, string>(
                "ChapterSegmentation.MinHeadingScore",
                request.Options.ChapterSegmentation.MinHeadingScore.ToString("0.###")));
            diagnostics.Add(new KeyValuePair<string, string>(
                "ChapterSegmentation.MaxHeadingWords",
                request.Options.ChapterSegmentation.MaxHeadingWords.ToString()));
            diagnostics.Add(new KeyValuePair<string, string>(
                "ChapterSegmentation.MaxHeadingLength",
                request.Options.ChapterSegmentation.MaxHeadingLength.ToString()));
        }

        foreach (var phaseTiming in phaseTimings)
        {
            diagnostics.Add(new KeyValuePair<string, string>(
                $"Phase.{phaseTiming.Name}",
                phaseTiming.Duration.ToString(@"mm\:ss\.fff")));
        }

        return diagnostics;
    }
}
