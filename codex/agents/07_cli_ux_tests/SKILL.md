---
name: cli-ux-tests
description: >
  Phase 5 agent — wires CLI UX (Spectre.Console progress bar, System.CommandLine),
  implements error handling/sanitization, performance profiling, and acceptance tests.
---

# Agent 07 — CLI UX, Error Handling & Acceptance Tests

## Scope

Polish the CLI experience, add error handling with content sanitization,
profile performance, and verify acceptance criteria.

## Inputs

- All pipeline components from Agents 01–06
- `codex/SKILL.md` for exit codes, constraints, acceptance tests
- `codex/workflow.md` Phase 5

## Outputs

- Updated `Program.cs` — full CLI wiring with progress bar
- `Helpers/ExceptionSanitizer.cs` — finalized
- Integration tests / smoke tests
- Final single-file publish

## Acceptance Criteria

1. CLI parses arguments correctly (`source_pdf`, `target_pdf`, `--output`, `--config`, `--threshold`)
2. Spectre.Console progress bar shows 5 phases with status messages
3. Exit codes: 0 (success), 1 (error), 2 (invalid args)
4. All exceptions are sanitized — no document content in error messages
5. File path validation: exists, is `.pdf`, readable
6. No temp files remain after execution
7. `dotnet build --configuration Release --warnaserror` — 0 errors, 0 warnings
8. `dotnet test --configuration Release` — all pass
9. Single-file publish produces working EXE

## Detailed Instructions

### Program.cs — Full CLI Wiring

```csharp
using System.CommandLine;
using Spectre.Console;

var sourcePdfArg = new Argument<FileInfo>("source_pdf", "Source PDF file path");
var targetPdfArg = new Argument<FileInfo>("target_pdf", "Target PDF file path");
var outputOption = new Option<string>("--output", () => ".\\diff_report.xlsx", "Output Excel path");
outputOption.AddAlias("-o");
var configOption = new Option<string?>("--config", "External JSON config path");
configOption.AddAlias("-c");
var thresholdOption = new Option<double>("--threshold", () => 0.85, "Similarity threshold");

var rootCommand = new RootCommand("PdfSpecDiffReporter — PDF specification diff tool")
{
    sourcePdfArg, targetPdfArg, outputOption, configOption, thresholdOption
};

rootCommand.SetHandler(async (source, target, output, config, threshold) =>
{
    try
    {
        // Validate inputs
        if (!source.Exists)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Source PDF not found.");
            Environment.ExitCode = 2;
            return;
        }
        if (!target.Exists)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Target PDF not found.");
            Environment.ExitCode = 2;
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                // Phase 1
                var task1 = ctx.AddTask("[bold][1/5][/] Loading PDFs...");
                using var sourceStream = SecureIngestion.LoadToMemory(source.FullName);
                using var targetStream = SecureIngestion.LoadToMemory(target.FullName);
                var sourcePages = PdfTextExtractor.ExtractPages(sourceStream);
                var targetPages = PdfTextExtractor.ExtractPages(targetStream);
                task1.Value = 100;

                // Phase 2
                var task2 = ctx.AddTask("[bold][2/5][/] Cleaning text...");
                sourcePages = TextCleanup.RemoveHeadersFooters(sourcePages);
                targetPages = TextCleanup.RemoveHeadersFooters(targetPages);
                task2.Value = 100;

                // Phase 3
                var task3 = ctx.AddTask("[bold][3/5][/] Matching chapters...");
                var sourceChapters = ChapterSegmenter.Segment(sourcePages);
                var targetChapters = ChapterSegmenter.Segment(targetPages);
                var pairs = ChapterMatcher.Match(sourceChapters, targetChapters);
                task3.Value = 100;

                // Phase 4
                var task4 = ctx.AddTask("[bold][4/5][/] Computing differences...");
                var diffs = DiffEngine.ComputeDiffs(pairs, threshold);
                task4.Value = 100;

                // Phase 5
                var task5 = ctx.AddTask("[bold][5/5][/] Generating report...");
                stopwatch.Stop();

                var outputPath = output.Contains("{Timestamp}")
                    ? output.Replace("{Timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                    : output;

                ExcelReporter.Generate(
                    outputPath,
                    source.Name, target.Name,
                    pairs, diffs,
                    stopwatch.Elapsed
                );
                task5.Value = 100;

                AnsiConsole.MarkupLine(
                    $"\n[green]✓ Report saved:[/] {outputPath}");
                AnsiConsole.MarkupLine(
                    $"[dim]Processed in {stopwatch.Elapsed:mm\\:ss\\.fff}[/]");
            });

        Environment.ExitCode = 0;
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine(
            $"[red]Error:[/] {ExceptionSanitizer.Sanitize(ex)}");
        Environment.ExitCode = 1;
    }
    finally
    {
        GC.Collect();
    }
},
sourcePdfArg, targetPdfArg, outputOption, configOption, thresholdOption);

return await rootCommand.InvokeAsync(args);
```

### ExceptionSanitizer (Final Version)

```csharp
public static class ExceptionSanitizer
{
    public static string Sanitize(Exception ex)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        return ex switch
        {
            FileNotFoundException => $"File not found. [Ref: {correlationId}]",
            IOException => $"File access error. [Ref: {correlationId}]",
            UnauthorizedAccessException => $"File permission denied. [Ref: {correlationId}]",
            ArgumentException => $"Invalid argument. [Ref: {correlationId}]",
            OutOfMemoryException => $"Insufficient memory. [Ref: {correlationId}]",
            _ => $"Unexpected error ({ex.GetType().Name}). [Ref: {correlationId}]"
        };
        // NEVER include ex.Message — it may contain document content
    }
}
```

### File Path Validation

```csharp
public static class InputValidator
{
    public static (bool valid, string? error) ValidatePdfPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Path is empty.");
        if (!File.Exists(path))
            return (false, "File does not exist.");
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return (false, "File is not a PDF.");
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            return (true, null);
        }
        catch
        {
            return (false, "File is not readable.");
        }
    }
}
```

## Edge Cases

- **Missing PDF**: Clear error message, exit code 2
- **Corrupt PDF**: PdfPig exception caught → sanitized message, exit code 1
- **Output directory doesn't exist**: Create it before writing
- **User presses Ctrl+C during processing**: Graceful shutdown, no temp files
- **Very large PDFs**: Monitor memory, log phase timing (not content)
- **Read-only output path**: Catch `UnauthorizedAccessException`
- **Empty PDFs**: Process normally → empty report

## Never Do

- ❌ Print `ex.Message` or `ex.StackTrace` to console (may contain doc content)
- ❌ Create temp files during any phase
- ❌ Log extracted text, chapter content, or diff text
- ❌ Make network calls
- ❌ Leave `FileStream` handles open on error paths

## Suggested Unit Tests

```csharp
[Fact]
public void ExceptionSanitizer_NeverExposesMessage()
{
    var exceptions = new Exception[]
    {
        new InvalidOperationException("secret content from PDF page 5"),
        new FormatException("Error parsing 'Chapter 1: Confidential'"),
        new ArgumentException("Invalid value: 'TOP SECRET DATA'")
    };

    foreach (var ex in exceptions)
    {
        var sanitized = ExceptionSanitizer.Sanitize(ex);
        Assert.DoesNotContain("secret", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidential", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOP SECRET", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ref:", sanitized);
    }
}

[Fact]
public void InputValidator_MissingFile_ReturnsFalse()
{
    var (valid, _) = InputValidator.ValidatePdfPath("nonexistent.pdf");
    Assert.False(valid);
}

[Fact]
public void InputValidator_NonPdfExtension_ReturnsFalse()
{
    var tempFile = Path.GetTempFileName(); // .tmp extension
    try
    {
        var (valid, _) = InputValidator.ValidatePdfPath(tempFile);
        Assert.False(valid);
    }
    finally { File.Delete(tempFile); }
}

[Fact]
public void InputValidator_ValidPdf_ReturnsTrue()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "test.pdf");
    File.WriteAllBytes(tempPath, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
    try
    {
        var (valid, _) = InputValidator.ValidatePdfPath(tempPath);
        Assert.True(valid);
    }
    finally { File.Delete(tempPath); }
}

[Fact]
public void ExitCodes_InvalidArgs_Returns2()
{
    // Test by invoking the CLI with missing arguments
    // and verifying Environment.ExitCode == 2
}
```
