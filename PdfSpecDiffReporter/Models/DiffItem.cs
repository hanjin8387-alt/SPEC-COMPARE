using System;

namespace PdfSpecDiffReporter.Models;

public sealed class DiffItem
{
    private string _beforeText = string.Empty;
    private string _afterText = string.Empty;

    public string ChapterKey { get; init; } = string.Empty;

    public ChangeType ChangeType { get; init; }

    public string BeforeText
    {
        get => _beforeText;
        init => _beforeText = Truncate(value);
    }

    public string AfterText
    {
        get => _afterText;
        init => _afterText = Truncate(value);
    }

    public double SimilarityScore { get; init; }

    public string PageRef { get; init; } = string.Empty;

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= Constants.MaxExcerptLength
            ? value
            : value[..Constants.MaxExcerptLength];
    }
}
