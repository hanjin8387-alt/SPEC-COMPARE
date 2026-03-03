using System.Diagnostics;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ChapterMatcherTests
{
    [Fact]
    public void Match_PrioritizesExactKeyMatches()
    {
        var source = new List<ChapterNode>
        {
            CreateChapter("1", "Original Title"),
            CreateChapter("2", "Source Only")
        };
        var target = new List<ChapterNode>
        {
            CreateChapter("1", "Completely Different Title"),
            CreateChapter("3", "Target Only")
        };

        var result = ChapterMatcher.Match(source, target, titleSimilarityThreshold: 0.95);

        var exactMatch = Assert.Single(result.Matches);
        Assert.Equal("1", exactMatch.Source?.Key);
        Assert.Equal("1", exactMatch.Target?.Key);
        Assert.Equal(1d, exactMatch.TitleSimilarity);
        Assert.Single(result.UnmatchedSource);
        Assert.Equal("2", result.UnmatchedSource[0].Key);
        Assert.Single(result.UnmatchedTarget);
        Assert.Equal("3", result.UnmatchedTarget[0].Key);
    }

    [Fact]
    public void Match_UsesTitleSimilarityForDifferentKeys()
    {
        var source = new List<ChapterNode>
        {
            CreateChapter("A", "Power Supply Requirement")
        };
        var target = new List<ChapterNode>
        {
            CreateChapter("B", "Power Supply Requirements")
        };

        var result = ChapterMatcher.Match(source, target, titleSimilarityThreshold: 0.7);

        var match = Assert.Single(result.Matches);
        Assert.Equal("A", match.Source?.Key);
        Assert.Equal("B", match.Target?.Key);
        Assert.True(match.TitleSimilarity >= 0.7);
        Assert.Empty(result.UnmatchedSource);
        Assert.Empty(result.UnmatchedTarget);
    }

    [Fact]
    public void Match_LeavesNodesUnmatchedWhenSimilarityIsTooLow()
    {
        var source = new List<ChapterNode>
        {
            CreateChapter("A", "Thermal Limits")
        };
        var target = new List<ChapterNode>
        {
            CreateChapter("B", "Pin Assignment")
        };

        var result = ChapterMatcher.Match(source, target, titleSimilarityThreshold: 0.9);

        Assert.Empty(result.Matches);
        Assert.Single(result.UnmatchedSource);
        Assert.Single(result.UnmatchedTarget);
    }

    [Fact]
    public void Match_ThrowsForNullArguments()
    {
        var nodes = new List<ChapterNode>();

        Assert.Throws<ArgumentNullException>(() => ChapterMatcher.Match(null!, nodes));
        Assert.Throws<ArgumentNullException>(() => ChapterMatcher.Match(nodes, null!));
    }

    [Fact]
    public void Match_WithDeeplyNestedChildren_FlattensCorrectly()
    {
        var sourceRoot = CreateChapter("1", "Root");
        var sourceChild = CreateChapter("1.1", "Child");
        var sourceGrandChild = CreateChapter("1.1.1", "Grand Child");
        sourceRoot.Children.Add(sourceChild);
        sourceChild.Children.Add(sourceGrandChild);

        var targetRoot = CreateChapter("1", "Root Updated");
        var targetChild = CreateChapter("1.1", "Child Updated");
        var targetGrandChild = CreateChapter("1.1.1", "Grand Child Updated");
        targetRoot.Children.Add(targetChild);
        targetChild.Children.Add(targetGrandChild);

        var result = ChapterMatcher.Match(new[] { sourceRoot }, new[] { targetRoot }, titleSimilarityThreshold: 0.99);

        Assert.Equal(3, result.Matches.Count);
        Assert.Contains(result.Matches, pair => pair.Source?.Key == "1" && pair.Target?.Key == "1");
        Assert.Contains(result.Matches, pair => pair.Source?.Key == "1.1" && pair.Target?.Key == "1.1");
        Assert.Contains(result.Matches, pair => pair.Source?.Key == "1.1.1" && pair.Target?.Key == "1.1.1");
        Assert.Empty(result.UnmatchedSource);
        Assert.Empty(result.UnmatchedTarget);
    }

    [Fact]
    public void Match_WithLargeInput_CompletesInReasonableTime()
    {
        const int chapterCount = 500;
        var source = Enumerable.Range(1, chapterCount)
            .Select(index => CreateChapter(index.ToString(), $"Section {index}"))
            .ToList();
        var target = Enumerable.Range(1, chapterCount)
            .Select(index => CreateChapter(index.ToString(), $"Section {index} Updated"))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var result = ChapterMatcher.Match(source, target);
        stopwatch.Stop();

        Assert.Equal(chapterCount, result.Matches.Count);
        Assert.Empty(result.UnmatchedSource);
        Assert.Empty(result.UnmatchedTarget);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(6), $"Expected matcher to finish quickly, but took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private static ChapterNode CreateChapter(string key, string title)
    {
        return new ChapterNode
        {
            Key = key,
            Title = title,
            Level = 1,
            PageStart = 1,
            PageEnd = 1
        };
    }
}
