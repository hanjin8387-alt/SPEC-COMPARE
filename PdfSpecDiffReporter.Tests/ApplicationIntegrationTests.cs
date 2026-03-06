using System;
using System.IO;
using ClosedXML.Excel;
using PdfSpecDiffReporter.Application;
using PdfSpecDiffReporter.Helpers;
using Spectre.Console;

namespace PdfSpecDiffReporter.Tests;

public sealed class ApplicationIntegrationTests
{
    [Fact]
    public void Run_WithSyntheticPdfInputs_GeneratesWorkbook()
    {
        var sourcePdf = TestPdfFactory.CreatePdf(
            new[]
            {
                new PdfTestLine("CHAPTER 1 OVERVIEW", 18d, 24d),
                new PdfTestLine("System shall initialize.", 12d, 18d),
                new PdfTestLine("CHAPTER 2 DETAILS", 18d, 24d),
                new PdfTestLine("Voltage shall be 5V.", 12d, 18d)
            });
        var targetPdf = TestPdfFactory.CreatePdf(
            new[]
            {
                new PdfTestLine("CHAPTER 1 OVERVIEW", 18d, 24d),
                new PdfTestLine("System shall initialize safely.", 12d, 18d),
                new PdfTestLine("CHAPTER 2 DETAILS", 18d, 24d),
                new PdfTestLine("Voltage shall be 5V.", 12d, 18d)
            });
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            var application = new SpecCompareApplication(AnsiConsole.Console);

            var exitCode = application.Run(
                new SpecCompareRequest(sourcePdf, targetPdf, outputPath, null, null, null),
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using var workbook = new XLWorkbook(outputPath);
            Assert.NotNull(workbook.Worksheet("Summary"));
            Assert.NotNull(workbook.Worksheet("ChangeDetails"));
            Assert.NotNull(workbook.Worksheet("FullText"));
            Assert.NotNull(workbook.Worksheet("MatchEvidence"));
            Assert.NotNull(workbook.Worksheet("Diagnostics"));
        }
        finally
        {
            DeleteIfExists(sourcePdf);
            DeleteIfExists(targetPdf);
            DeleteIfExists(outputPath);
        }
    }

    [Fact]
    public void Run_WhenCanceled_ReturnsRuntimeExitCodeWithoutWritingOutput()
    {
        var sourcePdf = TestPdfFactory.CreatePdf(
            new[]
            {
                new PdfTestLine("CHAPTER 1 OVERVIEW", 18d, 24d),
                new PdfTestLine("System shall initialize.", 12d, 18d)
            });
        var targetPdf = TestPdfFactory.CreatePdf(
            new[]
            {
                new PdfTestLine("CHAPTER 1 OVERVIEW", 18d, 24d),
                new PdfTestLine("System shall initialize safely.", 12d, 18d)
            });
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        try
        {
            var application = new SpecCompareApplication(AnsiConsole.Console);

            var exitCode = application.Run(
                new SpecCompareRequest(sourcePdf, targetPdf, outputPath, null, null, null),
                cancellationSource.Token);

            Assert.Equal(ExceptionSanitizer.CanceledExitCode, exitCode);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            DeleteIfExists(sourcePdf);
            DeleteIfExists(targetPdf);
            DeleteIfExists(outputPath);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
