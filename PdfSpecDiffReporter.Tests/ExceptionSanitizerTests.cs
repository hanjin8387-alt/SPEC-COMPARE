using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class ExceptionSanitizerTests
{
    [Fact]
    public void SecureIngestion_LoadToMemory_WhenFileMissing_ThrowsSanitizedException()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "PdfSpecDiffReporter.Tests",
            Guid.NewGuid().ToString("N"),
            "secret-input.pdf");

        var ex = Assert.Throws<InvalidOperationException>(() => SecureIngestion.LoadToMemory(missingPath));

        Assert.Matches(@"^Operation failed\. Correlation=[0-9a-f]{8}$", ex.Message);
        Assert.DoesNotContain("secret-input.pdf", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(missingPath, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExcelReporter_Generate_WhenWriteFails_ThrowsSanitizedExceptionWithoutSensitivePayload()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var sensitivePayload = "TOP_SECRET_PAYLOAD";
        var ex = Assert.Throws<InvalidOperationException>(() => ExcelReporter.Generate(
            outputPath: tempDirectory,
            sourceFileName: "source.pdf",
            targetFileName: "target.pdf",
            allPairs: new List<ChapterPair>(),
            diffs: new List<DiffItem>
            {
                new()
                {
                    ChapterKey = "1",
                    ChangeType = ChangeType.Modified,
                    BeforeText = sensitivePayload,
                    AfterText = "after",
                    SimilarityScore = 0.5,
                    PageRef = "p1-1"
                }
            },
            processingTime: TimeSpan.Zero));

        try
        {
            Assert.True(
                Regex.IsMatch(
                    ex.Message,
                    @"^Operation failed\. Correlation=[0-9a-f]{8}$",
                    RegexOptions.CultureInvariant),
                $"Unexpected sanitized format: {ex.Message}");
            Assert.DoesNotContain(sensitivePayload, ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tempDirectory, ex.Message, StringComparison.OrdinalIgnoreCase);
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
