using System;
using System.Diagnostics;
using System.IO;
using ClosedXML.Excel;
using PdfSpecDiffReporter.Application;

namespace PdfSpecDiffReporter.Tests;

public sealed class CliSmokeTests
{
    [Fact]
    public void Program_WithSyntheticInputs_ReturnsSuccessAndWritesWorkbook()
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
        var appAssemblyPath = typeof(SpecCompareApplication).Assembly.Location;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(appAssemblyPath) ?? Environment.CurrentDirectory
            };
            startInfo.ArgumentList.Add(appAssemblyPath);
            startInfo.ArgumentList.Add(sourcePdf);
            startInfo.ArgumentList.Add(targetPdf);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo);

            Assert.NotNull(process);
            Assert.True(process.WaitForExit(30000), "The CLI process did not finish within 30 seconds.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            Assert.Equal(0, process.ExitCode);
            Assert.True(File.Exists(outputPath), $"Expected workbook at '{outputPath}'. Stdout: {standardOutput}\nStderr: {standardError}");
            Assert.Contains("Report saved", standardOutput, StringComparison.OrdinalIgnoreCase);

            using var workbook = new XLWorkbook(outputPath);
            Assert.NotNull(workbook.Worksheet("Summary"));
            Assert.NotNull(workbook.Worksheet("ChangeDetails"));
            Assert.NotNull(workbook.Worksheet("Diagnostics"));
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
