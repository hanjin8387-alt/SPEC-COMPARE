using System.Collections.Generic;
using System.Linq;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class DiffEngineTests
{
    [Fact]
    public void ComputeDiffs_WhenParagraphIsSplit_ClassifiesAsAddDeleteWithoutFalseModified()
    {
        var pair = new ChapterPair(
            CreateNode("1", "Overview", "System shall start quickly."),
            CreateNode("1", "Overview", "System shall start.\n\nQuickly."),
            1d);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.85d);

        Assert.DoesNotContain(diffs, item => item.ChangeType == ChangeType.Modified);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted);
    }

    [Fact]
    public void ComputeDiffs_WhenParagraphsReordered_UsesAddDeleteInsteadOfFalseModified()
    {
        var pair = new ChapterPair(
            CreateNode("1", "Overview", "Alpha paragraph.\n\nBeta paragraph."),
            CreateNode("1", "Overview", "Beta paragraph.\n\nAlpha paragraph."),
            1d);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.85d);

        Assert.DoesNotContain(diffs, item => item.ChangeType == ChangeType.Modified);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted);
    }

    [Fact]
    public void ComputeDiffs_WhenChapterTitleChanges_EmitsTitleModification()
    {
        var pair = new ChapterPair(
            CreateNode("1", "Overview", "Same paragraph."),
            CreateNode("1", "System Overview", "Same paragraph."),
            0.5d);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.85d);

        var titleDiff = Assert.Single(diffs);
        Assert.Equal(ChangeType.Modified, titleDiff.ChangeType);
        Assert.Equal("Overview", titleDiff.BeforeText);
        Assert.Equal("System Overview", titleDiff.AfterText);
    }

    private static ChapterNode CreateNode(string key, string title, string content)
    {
        var node = new ChapterNode
        {
            Key = key,
            Title = title,
            Level = 1,
            PageStart = 1,
            PageEnd = 1
        };

        node.Content.Append(content);
        return node;
    }
}
