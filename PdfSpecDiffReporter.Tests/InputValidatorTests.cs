using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Tests;

public sealed class InputValidatorTests
{
    public static IEnumerable<object?[]> InvalidPdfInputs()
    {
        yield return new object?[] { null };
        yield return new object?[] { string.Empty };
        yield return new object?[] { "file.txt" };
        yield return new object?[] { Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"), "missing.pdf") };
    }

    public static IEnumerable<object[]> InvalidThresholdValues()
    {
        yield return new object[] { double.NaN };
        yield return new object[] { double.PositiveInfinity };
        yield return new object[] { 0d };
        yield return new object[] { -1d };
        yield return new object[] { 2d };
    }

    public static IEnumerable<object[]> ValidThresholdValues()
    {
        yield return new object[] { 0.01d };
        yield return new object[] { 0.5d };
        yield return new object[] { 1d };
    }

    [Theory]
    [MemberData(nameof(InvalidPdfInputs))]
    public void ValidatePdfPath_WithInvalidInputs(string? input)
    {
        var result = InputValidator.ValidatePdfPath(input, "Source PDF");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ValidatePdfPath_WithValidPdf_ReturnsValid()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"), "valid.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
        File.WriteAllBytes(tempFilePath, new byte[] { 1, 2, 3 });

        try
        {
            var result = InputValidator.ValidatePdfPath(tempFilePath, "Source PDF");

            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.IsIoRelated);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(tempFilePath)!);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidThresholdValues))]
    public void ValidateThreshold_WithInvalidValues(double threshold)
    {
        var result = InputValidator.ValidateThreshold(threshold);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Theory]
    [MemberData(nameof(ValidThresholdValues))]
    public void ValidateThreshold_WithValidValues(double threshold)
    {
        var result = InputValidator.ValidateThreshold(threshold);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateOptionalConfigPath_WithNull_ReturnsValid()
    {
        var result = InputValidator.ValidateOptionalConfigPath(null);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateOptionalConfigPath_WithWrongExtension_ReturnsInvalid()
    {
        var result = InputValidator.ValidateOptionalConfigPath("settings.txt");

        Assert.False(result.IsValid);
        Assert.Equal("Config file must use .json extension.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("report.txt")]
    public void ValidateOutputPath_WithInvalidInputs(string? outputPath)
    {
        var result = InputValidator.ValidateOutputPath(outputPath);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ValidateOutputPath_WithValid_ReturnsResolvedPath()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"));
        var requestedOutput = Path.Combine(tempDirectory, "report-{Timestamp}.xlsx");

        try
        {
            var result = InputValidator.ValidateOutputPath(requestedOutput);

            Assert.True(result.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(result.ResolvedPath));
            Assert.Equal(Path.GetFullPath(result.ResolvedPath!), result.ResolvedPath);
            Assert.EndsWith(".xlsx", result.ResolvedPath!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("{Timestamp}", result.ResolvedPath!, StringComparison.Ordinal);
            Assert.True(Directory.Exists(tempDirectory));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        catch
        {
            // Cleanup best effort only for tests.
        }
    }
}
