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
            CreateNode("1", "GENERAL"),
            CreateNode("1_dup1", "GENERAL")
        };

        var target = new List<ChapterNode>
        {
            CreateNode("1_dup1", "GENERAL"),
            CreateNode("1", "GENERAL")
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
    public void Match_HandlesSimpleReorderWithoutLosingPairs()
    {
        var source = new List<ChapterNode>
        {
            CreateNode("1", "Introduction"),
            CreateNode("2", "Scope")
        };

        var target = new List<ChapterNode>
        {
            CreateNode("2", "Scope"),
            CreateNode("1", "Introduction")
        };

        var result = ChapterMatcher.Match(source, target, 0.7d);

        Assert.Equal(2, result.Matches.Count);
        Assert.Empty(result.UnmatchedSource);
        Assert.Empty(result.UnmatchedTarget);
    }

    private static ChapterNode CreateNode(string key, string title)
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
