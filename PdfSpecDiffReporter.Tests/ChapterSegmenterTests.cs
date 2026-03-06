using System.Collections.Generic;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ChapterSegmenterTests
{
    [Fact]
    public void Segment_DoesNotPromoteMeasurementOrDateLinesToHeadings()
    {
        var pages = new List<PageText>
        {
            new(
                1,
                "1 10 mm clearance\n2 2026-03-04 revision\nbody line",
                new List<TextLine>())
        };

        var roots = ChapterSegmenter.Segment(pages, new ChapterSegmentationOptions());

        var root = Assert.Single(roots);
        Assert.Equal("0", root.Key);
        Assert.Equal("(Preamble)", root.Title);
        Assert.Contains("10 mm clearance", root.Content.ToString());
        Assert.Contains("2026-03-04", root.Content.ToString());
    }

    [Fact]
    public void Segment_UsesTocHintsToAcceptRealHeading()
    {
        var pages = new List<PageText>
        {
            new(1, "1 INTRODUCTION ........ 3", new List<TextLine>()),
            new(2, "CHAPTER 1 INTRODUCTION\nThis chapter starts here.", new List<TextLine>())
        };

        var roots = ChapterSegmenter.Segment(
            pages,
            new ChapterSegmentationOptions
            {
                TocScanPageCount = 2
            });

        Assert.Equal(2, roots.Count);
        Assert.Equal("(Preamble)", roots[0].Title);
        Assert.Equal("1", roots[1].Key);
        Assert.Equal("INTRODUCTION", roots[1].Title);
    }

    [Fact]
    public void Segment_UsesWeightedFontBaselineForMixedHeadingAndBodyLines()
    {
        var lines = new List<TextLine>
        {
            new(1, "CHAPTER 1 OVERVIEW", TextNormalizer.Normalize("CHAPTER 1 OVERVIEW"), 100d, 0d, 100d, 36d, 36d, 2),
            new(1, "This body line establishes the baseline for normal text.", TextNormalizer.Normalize("This body line establishes the baseline for normal text."), 80d, 0d, 200d, 12d, 12d, 20),
            new(1, "SECTION 2 DETAILS", TextNormalizer.Normalize("SECTION 2 DETAILS"), 60d, 0d, 100d, 18d, 18d, 2),
            new(1, "Detailed body content follows here.", TextNormalizer.Normalize("Detailed body content follows here."), 40d, 0d, 150d, 12d, 12d, 6)
        };

        var pages = new List<PageText>
        {
            new(
                1,
                "CHAPTER 1 OVERVIEW\nThis body line establishes the baseline for normal text.\nSECTION 2 DETAILS\nDetailed body content follows here.",
                lines)
        };

        var roots = ChapterSegmenter.Segment(pages, new ChapterSegmentationOptions());

        Assert.Collection(
            roots,
            chapter => Assert.Equal("1", chapter.Key),
            chapter => Assert.Equal("2", chapter.Key));
    }
}
