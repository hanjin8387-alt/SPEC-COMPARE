using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfSpecDiffReporter.Models;

public sealed class ChapterMatchResult
{
    public ChapterMatchResult(
        IReadOnlyList<ChapterPair> matches,
        IReadOnlyList<ChapterNode> unmatchedSource,
        IReadOnlyList<ChapterNode> unmatchedTarget)
    {
        Matches = matches ?? throw new ArgumentNullException(nameof(matches));
        UnmatchedSource = unmatchedSource ?? throw new ArgumentNullException(nameof(unmatchedSource));
        UnmatchedTarget = unmatchedTarget ?? throw new ArgumentNullException(nameof(unmatchedTarget));
        AllPairs = BuildAllPairs(matches, unmatchedSource, unmatchedTarget);
    }

    public IReadOnlyList<ChapterPair> Matches { get; }

    public IReadOnlyList<ChapterNode> UnmatchedSource { get; }

    public IReadOnlyList<ChapterNode> UnmatchedTarget { get; }

    public IReadOnlyList<ChapterPair> AllPairs { get; }

    private static IReadOnlyList<ChapterPair> BuildAllPairs(
        IReadOnlyList<ChapterPair> matches,
        IReadOnlyList<ChapterNode> unmatchedSource,
        IReadOnlyList<ChapterNode> unmatchedTarget)
    {
        var allPairs = new List<ChapterPair>(matches.Count + unmatchedSource.Count + unmatchedTarget.Count);
        allPairs.AddRange(matches);
        allPairs.AddRange(unmatchedSource.Select(node => new ChapterPair(node, null, null)));
        allPairs.AddRange(unmatchedTarget.Select(node => new ChapterPair(null, node, null)));
        return allPairs;
    }
}
