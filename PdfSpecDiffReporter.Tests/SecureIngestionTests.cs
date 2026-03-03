using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Pipeline;

namespace PdfSpecDiffReporter.Tests;

public sealed class SecureIngestionTests
{
    [Fact]
    public void LoadToMemory_WithValidFile_ReturnsMemoryStreamAtPositionZero()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"), "input.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
        File.WriteAllBytes(tempFilePath, new byte[] { 1, 2, 3, 4 });

        try
        {
            using var stream = SecureIngestion.LoadToMemory(tempFilePath);

            Assert.Equal(0, stream.Position);
            Assert.Equal(4, stream.Length);
            Assert.Equal(1, stream.ReadByte());
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(tempFilePath)!);
        }
    }

    [Fact]
    public void LoadToMemory_WithEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SecureIngestion.LoadToMemory(" "));
    }

    [Fact]
    public void LoadToMemory_WithMissingFile_ThrowsSanitizedException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"), "missing.pdf");

        var ex = Assert.Throws<InvalidOperationException>(() => SecureIngestion.LoadToMemory(missingPath));

        Assert.Matches(new Regex("^Operation failed\\. Correlation=[0-9a-f]{8}$", RegexOptions.CultureInvariant), ex.Message);
        Assert.DoesNotContain("missing.pdf", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadToMemory_ReleasesFileHandleAfterLoad()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), "PdfSpecDiffReporter.Tests", Guid.NewGuid().ToString("N"), "input.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
        File.WriteAllBytes(tempFilePath, new byte[] { 7, 8, 9 });

        try
        {
            using var stream = SecureIngestion.LoadToMemory(tempFilePath);
            using var exclusiveHandle = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            Assert.True(exclusiveHandle.CanRead);
        }
        finally
        {
            TryDeleteDirectory(Path.GetDirectoryName(tempFilePath)!);
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
