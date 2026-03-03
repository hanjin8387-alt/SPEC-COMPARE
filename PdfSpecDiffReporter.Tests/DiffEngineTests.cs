using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class DiffEngineTests
{
    [Fact]
    public void ComputeDiffs_ProducesModifiedWhenSimilarityMeetsThreshold()
    {
        var source = CreateChapter("1", "Power consumption is 10W.", pageStart: 1, pageEnd: 1);
        var target = CreateChapter("1", "Power consumption is 12W.", pageStart: 1, pageEnd: 2);
        var pairs = new List<ChapterPair> { new(source, target, 1d) };

        var diffs = DiffEngine.ComputeDiffs(pairs, similarityThreshold: 0.3);

        var item = Assert.Single(diffs);
        Assert.Equal(ChangeType.Modified, item.ChangeType);
        Assert.Equal("1", item.ChapterKey);
        Assert.Equal("Power consumption is 10W.", item.BeforeText);
        Assert.Equal("Power consumption is 12W.", item.AfterText);
        Assert.Equal("p1-2", item.PageRef);
        Assert.InRange(item.SimilarityScore, 0d, 1d);
    }

    [Fact]
    public void ComputeDiffs_ProducesAddedAndDeletedWhenSimilarityIsBelowThreshold()
    {
        var source = CreateChapter("1", "Alpha", pageStart: 4, pageEnd: 4);
        var target = CreateChapter("2", "Completely different paragraph", pageStart: 4, pageEnd: 4);
        var pairs = new List<ChapterPair> { new(source, target, 0d) };

        var diffs = DiffEngine.ComputeDiffs(pairs, similarityThreshold: 0.95);

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted && item.ChapterKey == "1");
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added && item.ChapterKey == "2");
        Assert.All(diffs, item => Assert.Equal("p4-4", item.PageRef));
    }

    [Fact]
    public void ComputeDiffs_HandlesUnmatchedSourceAndTargetPairs()
    {
        var sourceOnly = CreateChapter("10", "Legacy paragraph");
        var targetOnly = CreateChapter("11", "New paragraph");
        var pairs = new List<ChapterPair>
        {
            new(sourceOnly, null, 0d),
            new(null, targetOnly, 0d),
            new(null, null, 0d)
        };

        var diffs = DiffEngine.ComputeDiffs(pairs);

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted && item.ChapterKey == "10" && item.BeforeText.Contains("Legacy", StringComparison.Ordinal));
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added && item.ChapterKey == "11" && item.AfterText.Contains("New", StringComparison.Ordinal));
    }

    [Fact]
    public void ComputeDiffs_FromMatchResultIncludesUnmatchedNodes()
    {
        var matchResult = new ChapterMatchResult();
        matchResult.Matches.Add(new ChapterPair(
            CreateChapter("1", "Common paragraph"),
            CreateChapter("1", "Common paragraph updated"),
            1d));
        matchResult.UnmatchedSource.Add(CreateChapter("2", "Removed chapter text"));
        matchResult.UnmatchedTarget.Add(CreateChapter("3", "Added chapter text"));

        var diffs = DiffEngine.ComputeDiffs(matchResult);

        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted && item.ChapterKey == "2");
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added && item.ChapterKey == "3");
    }

    [Fact]
    public void ComputeDiffs_ThrowsForNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => DiffEngine.ComputeDiffs((IReadOnlyList<ChapterPair>)null!));
        Assert.Throws<ArgumentNullException>(() => DiffEngine.ComputeDiffs((ChapterMatchResult)null!));
    }

    [Fact]
    public void ComputeDiffs_WithEmptyContentChapters_ReturnsNoDiffs()
    {
        var source = CreateChapter("1", string.Empty);
        var target = CreateChapter("1", string.Empty);
        var pairs = new List<ChapterPair> { new(source, target, 1d) };

        var diffs = DiffEngine.ComputeDiffs(pairs, similarityThreshold: 0.5);

        Assert.Empty(diffs);
    }

    [Fact]
    public void ComputeDiffs_WithThresholdAtBoundary()
    {
        var before = "Voltage shall be 5V.";
        var after = "Voltage shall be 6V.";
        var source = CreateChapter("1", before, pageStart: 2, pageEnd: 2);
        var target = CreateChapter("1", after, pageStart: 2, pageEnd: 2);
        var threshold = SimilarityCalculator.Calculate(before, after);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { new(source, target, 1d) }, similarityThreshold: threshold);

        var item = Assert.Single(diffs);
        Assert.Equal(ChangeType.Modified, item.ChangeType);
        Assert.Equal("1", item.ChapterKey);
        Assert.True(Math.Abs(item.SimilarityScore - threshold) < 0.0000001d);
        Assert.Equal("p2-2", item.PageRef);
    }

    private static ChapterNode CreateChapter(
        string key,
        string paragraph,
        int pageStart = 1,
        int pageEnd = 1)
    {
        var node = new ChapterNode
        {
            Key = key,
            Title = $"Title {key}",
            Level = 1,
            PageStart = pageStart,
            PageEnd = pageEnd
        };
        node.Content.Append(paragraph);
        return node;
    }
}
