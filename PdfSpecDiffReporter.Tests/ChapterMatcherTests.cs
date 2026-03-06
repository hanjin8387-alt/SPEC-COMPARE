using System.Collections.Generic;
using System.Linq;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ChapterMatcherTests
{
    [Fact]
    public void Match_IsDeterministicForDuplicateTitlesWithReorderedTargets()
    {
        var source = new List<ChapterNode>
        {
            CreateNode("1", "GENERAL", order: 0, matchKey: "1"),
            CreateNode("1_dup1", "GENERAL", order: 1, matchKey: "1")
        };

        var target = new List<ChapterNode>
        {
            CreateNode("1_dup1", "GENERAL", order: 0, matchKey: "1"),
            CreateNode("1", "GENERAL", order: 1, matchKey: "1")
        };

        var first = ChapterMatcher.Match(source, target, 0.7d);
        var second = ChapterMatcher.Match(source, target, 0.7d);

        var firstPairs = first.Matches.Select(pair => $"{pair.Source!.Key}->{pair.Target!.Key}").ToList();
        var secondPairs = second.Matches.Select(pair => $"{pair.Source!.Key}->{pair.Target!.Key}").ToList();

        Assert.Equal(firstPairs, secondPairs);
        Assert.Equal(2, first.Matches.Count);
        Assert.Empty(first.UnmatchedSource);
        Assert.Empty(first.UnmatchedTarget);
    }

    [Fact]
    public void Match_AnchorsByExactKeyEvenWhenTitleChanges()
    {
        var source = new List<ChapterNode>
        {
            CreateNode("1", "Introduction", order: 0, matchKey: "1")
        };

        var target = new List<ChapterNode>
        {
            CreateNode("1", "System Overview", order: 0, matchKey: "1")
        };

        var result = ChapterMatcher.Match(source, target, 0.7d);

        var match = Assert.Single(result.Matches);
        Assert.Equal(ChapterMatchKind.ExactKeyAnchor, match.Evidence!.Kind);
        Assert.Empty(result.UnmatchedSource);
        Assert.Empty(result.UnmatchedTarget);
    }

    [Fact]
    public void Match_UsesParentContextToAvoidCrossMatchingDuplicateChildTitles()
    {
        var source = new List<ChapterNode>
        {
            CreateNode("A", "Scope", order: 0, matchKey: "1.1", parentMatchKey: "1", parentTitle: "Overview", level: 2),
            CreateNode("B", "Scope", order: 1, matchKey: "2.1", parentMatchKey: "2", parentTitle: "Safety", level: 2)
        };

        var target = new List<ChapterNode>
        {
            CreateNode("Y", "Scope", order: 0, matchKey: "20.1", parentMatchKey: "20", parentTitle: "Safety", level: 2),
            CreateNode("X", "Scope", order: 1, matchKey: "10.1", parentMatchKey: "10", parentTitle: "Overview", level: 2)
        };

        var result = ChapterMatcher.Match(source, target, 0.50d);

        Assert.Collection(
            result.Matches.OrderBy(pair => pair.Source!.Order),
            pair =>
            {
                Assert.Equal("A", pair.Source!.Key);
                Assert.Equal("X", pair.Target!.Key);
            },
            pair =>
            {
                Assert.Equal("B", pair.Source!.Key);
                Assert.Equal("Y", pair.Target!.Key);
            });
    }

    private static ChapterNode CreateNode(
        string key,
        string title,
        int order,
        string matchKey,
        string parentMatchKey = "",
        string parentTitle = "",
        int level = 1)
    {
        return new ChapterNode
        {
            Key = key,
            MatchKey = matchKey,
            Title = title,
            Level = level,
            Content = string.Empty,
            PageStart = 1,
            PageEnd = 1,
            Order = order,
            ParentMatchKey = parentMatchKey,
            ParentTitle = parentTitle,
            ParentKey = parentMatchKey
        };
    }
}
