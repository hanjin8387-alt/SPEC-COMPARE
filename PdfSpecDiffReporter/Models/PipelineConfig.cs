namespace PdfSpecDiffReporter.Models;

public sealed class PipelineConfig
{
    public double? DiffThreshold { get; init; }

    public double? ChapterMatchThreshold { get; init; }

    public TextNormalizationOptions? TextNormalization { get; init; }

    public ChapterSegmentationOptions? ChapterSegmentation { get; init; }

    public ReportOptions? Reporting { get; init; }
}

public sealed class TextNormalizationOptions
{
    public double HeaderFooterBandPercent { get; init; } = 0.10d;

    public int MinRepeatingPages { get; init; } = 3;

    public double RepeatingSimilarityThreshold { get; init; } = 0.90d;

    public double LineMergeTolerance { get; init; } = 2.0d;

    public int ZoneLineLimit { get; init; } = 3;

    public int SearchWindow { get; init; } = 8;

    public int MinZoneTextLength { get; init; } = 4;
}

public sealed class ChapterSegmentationOptions
{
    public int TocScanPageCount { get; init; } = 5;

    public double LayoutHeadingFontRatio { get; init; } = 1.08d;

    public double MinHeadingScore { get; init; } = 0.65d;

    public int MaxHeadingWords { get; init; } = 16;

    public int MaxHeadingLength { get; init; } = 120;
}
