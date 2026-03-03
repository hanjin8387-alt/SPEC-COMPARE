using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Tests;

public sealed class SimilarityCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsOneWhenBothInputsAreMissing()
    {
        Assert.Equal(1d, SimilarityCalculator.Calculate(null, null));
        Assert.Equal(1d, SimilarityCalculator.Calculate(string.Empty, string.Empty));
    }

    [Fact]
    public void Calculate_ReturnsZeroWhenOnlyOneInputExists()
    {
        Assert.Equal(0d, SimilarityCalculator.Calculate("alpha", null));
        Assert.Equal(0d, SimilarityCalculator.Calculate(null, "alpha"));
    }

    [Fact]
    public void Calculate_ReturnsOneForExactMatch()
    {
        var score = SimilarityCalculator.Calculate("exact text", "exact text");

        Assert.Equal(1d, score);
    }

    [Fact]
    public void Calculate_ComputesDeterministicLevenshteinScore()
    {
        var score = SimilarityCalculator.Calculate("kitten", "sitting");

        Assert.Equal(4d / 7d, score, precision: 6);
    }
}
