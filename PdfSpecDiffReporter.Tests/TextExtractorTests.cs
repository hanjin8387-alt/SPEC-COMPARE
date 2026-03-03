using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class TextExtractorTests
{
    [Fact]
    public void ExtractPages_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextExtractor.ExtractPages(null!));
    }

    [Fact]
    public void ExtractPages_WithEmptyStream_ThrowsOrReturnsEmpty()
    {
        using var stream = new MemoryStream();

        List<PdfSpecDiffReporter.Models.PageText>? pages = null;
        var exception = Record.Exception(() => pages = TextExtractor.ExtractPages(stream));

        if (exception is null)
        {
            Assert.Empty(pages!);
            return;
        }

        if (exception is InvalidOperationException wrapped)
        {
            Assert.Matches(new Regex("^Operation failed\\. Correlation=[0-9a-f]{8}$", RegexOptions.CultureInvariant), wrapped.Message);
            return;
        }

        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }
}
