using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ChapterSegmenterTests
{
    [Fact]
    public void Segment_BuildsHierarchyAndAssignsContent()
    {
        var pages = new List<PageText>
        {
            new(
                1,
                "1 Intro\nintro paragraph\n1.1 Scope\nscope paragraph\n2 End\nend paragraph",
                Array.Empty<WordInfo>())
        };

        var roots = ChapterSegmenter.Segment(pages);

        Assert.Equal(2, roots.Count);
        Assert.Equal("1", roots[0].Key);
        Assert.Equal("Intro", roots[0].Title);
        Assert.Contains("intro paragraph", roots[0].Content.ToString(), StringComparison.Ordinal);

        Assert.Single(roots[0].Children);
        Assert.Equal("1.1", roots[0].Children[0].Key);
        Assert.Equal("Scope", roots[0].Children[0].Title);
        Assert.Contains("scope paragraph", roots[0].Children[0].Content.ToString(), StringComparison.Ordinal);

        Assert.Equal("2", roots[1].Key);
        Assert.Equal("End", roots[1].Title);
        Assert.Contains("end paragraph", roots[1].Content.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_CreatesPreambleForTextBeforeFirstHeading()
    {
        var pages = new List<PageText>
        {
            new(
                1,
                "overview line\nCHAPTER 1: Start\nchapter content",
                Array.Empty<WordInfo>())
        };

        var roots = ChapterSegmenter.Segment(pages);

        Assert.Equal(2, roots.Count);
        Assert.Equal("0", roots[0].Key);
        Assert.Equal("(Preamble)", roots[0].Title);
        Assert.Contains("overview line", roots[0].Content.ToString(), StringComparison.Ordinal);

        Assert.Equal("1", roots[1].Key);
        Assert.Equal("Start", roots[1].Title);
    }

    [Fact]
    public void Segment_AppendsDuplicateSuffixForRepeatedChapterKeys()
    {
        var pages = new List<PageText>
        {
            new(1, "1 Intro\nfirst block", Array.Empty<WordInfo>()),
            new(2, "1 Intro Again\nsecond block", Array.Empty<WordInfo>())
        };

        var roots = ChapterSegmenter.Segment(pages);

        Assert.Equal(2, roots.Count);
        Assert.Equal("1", roots[0].Key);
        Assert.Equal("1_dup1", roots[1].Key);
    }

    [Fact]
    public void Segment_ThrowsForNullPages()
    {
        Assert.Throws<ArgumentNullException>(() => ChapterSegmenter.Segment(null!));
    }
}
