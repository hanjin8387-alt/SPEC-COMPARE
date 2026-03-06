using System;
using System.Collections.Generic;

namespace PdfSpecDiffReporter.Models;

public sealed class ChapterNode
{
    public string Key { get; init; } = string.Empty;

    public string MatchKey { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int Level { get; init; }

    public IReadOnlyList<TextBlock> Blocks { get; init; } = Array.Empty<TextBlock>();

    public string Content => TextBlock.CombineOriginalText(Blocks);

    public IReadOnlyList<ChapterNode> Children { get; init; } = Array.Empty<ChapterNode>();

    public int PageStart { get; init; }

    public int PageEnd { get; init; }

    public int Order { get; init; }

    public string ParentKey { get; init; } = string.Empty;

    public string ParentMatchKey { get; init; } = string.Empty;

    public string ParentTitle { get; init; } = string.Empty;
}
