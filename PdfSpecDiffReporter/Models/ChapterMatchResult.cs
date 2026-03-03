namespace PdfSpecDiffReporter.Models;

public sealed class ChapterMatchResult
{
    public List<ChapterPair> Matches { get; } = new();

    public List<ChapterNode> UnmatchedSource { get; } = new();

    public List<ChapterNode> UnmatchedTarget { get; } = new();
}
