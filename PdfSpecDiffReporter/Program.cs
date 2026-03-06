using System.CommandLine;
using PdfSpecDiffReporter.Application;
using Spectre.Console;

var sourcePdfArgument = new Argument<string>(
    "source_pdf")
{
    Description = "Path to the source PDF."
};

var targetPdfArgument = new Argument<string>(
    "target_pdf")
{
    Description = "Path to the target PDF."
};

var outputOption = new Option<string>(
    name: "--output",
    aliases: new[] { "-o" })
{
    Description = "Path to the generated diff report (.xlsx).",
    DefaultValueFactory = _ => "diff_report.xlsx"
};

var configOption = new Option<string?>(
    name: "--config",
    aliases: new[] { "-c" })
{
    Description = "Optional JSON config file path."
};

var diffThresholdOption = new Option<double?>(
    "--diff-threshold")
{
    Description = "Similarity threshold for diff classification (0 < value <= 1, default 0.85)."
};

var chapterMatchThresholdOption = new Option<double?>(
    "--chapter-match-threshold")
{
    Description = "Similarity threshold for chapter matching (0 < value <= 1, default 0.7)."
};

var rootCommand = new RootCommand("PdfSpecDiffReporter - PDF specification diff tool.");
rootCommand.Add(sourcePdfArgument);
rootCommand.Add(targetPdfArgument);
rootCommand.Add(outputOption);
rootCommand.Add(configOption);
rootCommand.Add(diffThresholdOption);
rootCommand.Add(chapterMatchThresholdOption);

var application = new SpecCompareApplication(AnsiConsole.Console);

rootCommand.SetAction(
    parseResult =>
    {
        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancellationHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        Console.CancelKeyPress += cancellationHandler;

        try
        {
            var request = new SpecCompareRequest(
                parseResult.GetRequiredValue(sourcePdfArgument),
                parseResult.GetRequiredValue(targetPdfArgument),
                parseResult.GetValue(outputOption) ?? "diff_report.xlsx",
                parseResult.GetValue(configOption),
                parseResult.GetValue(diffThresholdOption),
                parseResult.GetValue(chapterMatchThresholdOption));

            return application.Run(request, cancellationSource.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancellationHandler;
        }
    });

return await rootCommand.Parse(args).InvokeAsync();
