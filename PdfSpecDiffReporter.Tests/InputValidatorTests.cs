using System;
using System.IO;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Tests;

public sealed class InputValidatorTests
{
    [Fact]
    public void ValidatePdfPath_AcceptsPdfSignature()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            File.WriteAllBytes(path, "%PDF-1.7\n"u8.ToArray());
            var result = InputValidator.ValidatePdfPath(path, "Source PDF");

            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ValidatePdfPath_RejectsInvalidSignature()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            File.WriteAllText(path, "not a pdf");
            var result = InputValidator.ValidatePdfPath(path, "Source PDF");

            Assert.False(result.IsValid);
            Assert.Equal("Source PDF is not a valid PDF file.", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.1d)]
    [InlineData(1.1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ValidateSimilarityThreshold_RejectsOutOfRangeValues(double threshold)
    {
        var result = InputValidator.ValidateSimilarityThreshold(threshold, "Diff threshold");

        Assert.False(result.IsValid);
        Assert.Equal("Diff threshold must be greater than 0 and less than or equal to 1.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateOutputPath_DoesNotCreateDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var nestedDirectory = Path.Combine(root, "a", "b");
        var outputPath = Path.Combine(nestedDirectory, "report.xlsx");

        try
        {
            var result = InputValidator.ValidateOutputPath(outputPath);

            Assert.True(result.IsValid);
            Assert.NotNull(result.ResolvedPath);
            Assert.False(Directory.Exists(nestedDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateTextNormalizationOptions_RejectsOutOfRangeBandPercent()
    {
        var options = new TextNormalizationOptions
        {
            HeaderFooterBandPercent = 0.75d
        };

        var result = InputValidator.ValidateTextNormalizationOptions(options);

        Assert.False(result.IsValid);
        Assert.Equal("textNormalization.headerFooterBandPercent must be between 0 and 0.5.", result.ErrorMessage);
    }
}
