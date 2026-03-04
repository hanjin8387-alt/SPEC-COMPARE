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
                new List<WordInfo>())
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
            new(1, "1 INTRODUCTION ........ 3", new List<WordInfo>()),
            new(2, "CHAPTER 1 INTRODUCTION\nThis chapter starts here.", new List<WordInfo>())
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
}
