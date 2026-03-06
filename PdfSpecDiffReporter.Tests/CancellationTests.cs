using System;
using System.Collections.Generic;
using System.IO;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class CancellationTests
{
    [Fact]
    public void ChapterMatcher_ThrowsWhenCanceled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var nodes = new List<ChapterNode>
        {
            CreateNode("1", "Overview", 0),
            CreateNode("2", "Details", 1)
        };

        Assert.Throws<OperationCanceledException>(() => ChapterMatcher.Match(nodes, nodes, 0.7d, cancellationSource.Token));
    }

    [Fact]
    public void ExcelReporter_ThrowsWhenCanceled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        var pairs = new List<ChapterPair>
        {
            new(CreateNode("1", "Overview", 0), CreateNode("1", "Overview", 0), null)
        };
        var diffs = new List<DiffItem>
        {
            new("1", ChangeType.Modified, "before", "after", 0.8d, "p.1")
        };

        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                ExcelReporter.Generate(
                    outputPath,
                    "source.pdf",
                    "target.pdf",
                    pairs,
                    diffs,
                    () => TimeSpan.Zero,
                    cancellationToken: cancellationSource.Token));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static ChapterNode CreateNode(string key, string title, int order)
    {
        return new ChapterNode
        {
            Key = key,
            MatchKey = key,
            Title = title,
            Level = 1,
            Blocks = Array.Empty<TextBlock>(),
            PageStart = 1,
            PageEnd = 1,
            Order = order
        };
    }
}
