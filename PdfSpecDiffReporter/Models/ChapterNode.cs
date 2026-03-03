using System.Collections.Generic;
using System.Text;

namespace PdfSpecDiffReporter.Models;

public sealed class ChapterNode
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int Level { get; init; }

    public StringBuilder Content { get; } = new();

    public List<ChapterNode> Children { get; } = new();

    public int PageStart { get; set; }

    public int PageEnd { get; set; }
}
