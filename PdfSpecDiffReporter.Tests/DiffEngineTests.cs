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
            CreateNode("1", "Overview", "System shall start.\nQuickly."),
            null);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.85d);

        Assert.DoesNotContain(diffs, item => item.ChangeType == ChangeType.Modified);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Added);
        Assert.Contains(diffs, item => item.ChangeType == ChangeType.Deleted);
    }

    [Fact]
    public void ComputeDiffs_WhenParagraphsReordered_UsesAddDeleteInsteadOfFalseModified()
    {
        var pair = new ChapterPair(
            CreateNode("1", "Overview", "Alpha paragraph.\nBeta paragraph."),
            CreateNode("1", "Overview", "Beta paragraph.\nAlpha paragraph."),
            null);

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
            null);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.85d);

        var titleDiff = Assert.Single(diffs);
        Assert.Equal(ChangeType.Modified, titleDiff.ChangeType);
        Assert.Equal("Overview", titleDiff.BeforeText);
        Assert.Equal("System Overview", titleDiff.AfterText);
    }

    [Fact]
    public void ComputeDiffs_PreservesFullTextInDomainModel()
    {
        var before = new string('A', 640);
        var after = new string('B', 640);
        var pair = new ChapterPair(
            CreateNode("1", "Overview", before),
            CreateNode("1", "Overview", after),
            null);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.10d);

        Assert.Contains(diffs, diff => diff.ChangeType == ChangeType.Deleted && diff.BeforeText == before);
        Assert.Contains(diffs, diff => diff.ChangeType == ChangeType.Added && diff.AfterText == after);
        Assert.Contains(diffs, diff => diff.BeforeText.Length > 500 || diff.AfterText.Length > 500);
    }

    [Fact]
    public void ComputeDiffs_WhenDenseSectionHasSingleSentenceChange_EmitsLocalizedModification()
    {
        var pair = new ChapterPair(
            CreateNode("1", "Overview", "System shall initialize.\nThe module shall log events.\nShutdown shall be graceful."),
            CreateNode("1", "Overview", "System shall initialize.\nThe module shall log audit events.\nShutdown shall be graceful."),
            null);

        var diffs = DiffEngine.ComputeDiffs(new List<ChapterPair> { pair }, 0.70d);

        var modified = Assert.Single(diffs);
        Assert.Equal(ChangeType.Modified, modified.ChangeType);
        Assert.Contains("log events", modified.BeforeText);
        Assert.Contains("log audit events", modified.AfterText);
    }

    private static ChapterNode CreateNode(string key, string title, string content)
    {
        var lines = content
            .Split('\n')
            .Select((line, index) => new TextLine(
                1,
                line,
                TextNormalizer.Normalize(line),
                -index,
                0d,
                0d,
                0d,
                0d,
                0))
            .ToArray();
        var blocks = TextBlockBuilder.BuildBlocks(lines);

        return new ChapterNode
        {
            Key = key,
            MatchKey = key,
            Title = title,
            Level = 1,
            Blocks = blocks,
            PageStart = 1,
            PageEnd = 1,
            Order = 0
        };
    }
}
