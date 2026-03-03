using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Normalize_StandardizesWhitespaceAndControlCharacters()
    {
        var input = " \tA  B\r\n\r\n\rC\u0007D  ";

        var normalized = TextNormalizer.Normalize(input);

        Assert.Equal("A B\n\nCD", normalized);
    }

    [Fact]
    public void RemoveHeadersFooters_RemovesRepeatedHeaderFooterAndPageNumbers()
    {
        var pages = new List<PageText>
        {
            CreatePage(1, "Spec Header", "Body line 1", "Confidential Footer", "Page 1"),
            CreatePage(2, "Spec Header", "Body line 2", "Confidential Footer", "Page 2"),
            CreatePage(3, "Spec Header", "Body line 3", "Confidential Footer", "Page 3")
        };

        var cleaned = TextNormalizer.RemoveHeadersFooters(pages);

        Assert.Equal(3, cleaned.Count);
        Assert.Collection(
            cleaned,
            page => Assert.Equal("Body line 1", page.RawText),
            page => Assert.Equal("Body line 2", page.RawText),
            page => Assert.Equal("Body line 3", page.RawText));
    }

    [Fact]
    public void RemoveHeadersFooters_ThrowsForInvalidThreshold()
    {
        var pages = new List<PageText>
        {
            CreatePage(1, "Header", "Body", "Footer", "1")
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => TextNormalizer.RemoveHeadersFooters(pages, yThresholdPercent: 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextNormalizer.RemoveHeadersFooters(pages, yThresholdPercent: 0.5d));
    }

    [Fact]
    public void RemoveHeadersFooters_ThrowsForNullPages()
    {
        Assert.Throws<ArgumentNullException>(() => TextNormalizer.RemoveHeadersFooters(null!));
    }

    private static PageText CreatePage(
        int pageNumber,
        string headerLine,
        string bodyLine,
        string footerLine,
        string pageNumberLine)
    {
        var rawText = string.Join('\n', headerLine, bodyLine, footerLine, pageNumberLine);

        var words = new List<WordInfo>
        {
            new("Spec", 10d, 95d, 10d),
            new("Header", 35d, 95d, 10d),
            new("Body", 10d, 50d, 10d),
            new("line", 30d, 50d, 10d),
            new(pageNumber.ToString(), 50d, 50d, 10d),
            new("Confidential", 10d, 10d, 10d),
            new("Footer", 65d, 10d, 10d)
        };

        return new PageText(pageNumber, rawText, words);
    }
}
