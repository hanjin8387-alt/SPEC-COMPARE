using PdfSpecDiffReporter.Helpers;
using Spectre.Console;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecCompareOutcomePresenter
{
    private readonly IAnsiConsole _console;

    public SpecCompareOutcomePresenter(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public void WriteConfiguration(ResolvedSpecCompareRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConfigPath))
        {
            _console.MarkupLine($"[dim]Config: {Markup.Escape(Path.GetFullPath(request.ConfigPath))}[/]");
        }

        _console.MarkupLine(
            $"[dim]Diff threshold: {request.Options.DiffThreshold:0.###} | Chapter match threshold: {request.Options.ChapterMatchThreshold:0.###}[/]");
        _console.MarkupLine(
            $"[dim]FullText sheet: {request.Options.Reporting.IncludeFullTextSheet} | Preview length: {request.Options.Reporting.PreviewTextLength} | Diagnostics: {request.Options.Reporting.DiagnosticsVerbosity}[/]");
    }

    public void WriteValidationError(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "Invalid input." : message;
        _console.MarkupLine($"[red]Validation error:[/] {Markup.Escape(text)}");
    }

    public int WriteFailure(Exception exception)
    {
        var sanitized = ExceptionSanitizer.Sanitize(exception);
        var label = sanitized.ExitCode == ExceptionSanitizer.CanceledExitCode ? "Canceled" : "Error";
        var color = sanitized.ExitCode == ExceptionSanitizer.CanceledExitCode ? "yellow" : "red";
        _console.MarkupLine($"[{color}]{label}:[/] {Markup.Escape(sanitized.Message)}");
        return sanitized.ExitCode;
    }

    public void WriteSuccess(ComparisonRunResult runResult)
    {
        _console.MarkupLine($"\n[green]Report saved:[/] {Markup.Escape(runResult.OutputPath)}");
        _console.MarkupLine($"[dim]Processed in {runResult.ProcessingTime:mm\\:ss\\.fff}[/]");

        foreach (var phase in runResult.PhaseTimings)
        {
            _console.MarkupLine($"[dim]- {Markup.Escape(phase.Name)}: {phase.Duration:mm\\:ss\\.fff}[/]");
        }
    }
}
