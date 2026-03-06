using Spectre.Console;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecCompareApplication
{
    private readonly SpecCompareRequestResolver _requestResolver;
    private readonly SpecComparePipeline _pipeline;
    private readonly ConsoleProgressRunner _progressRunner;
    private readonly SpecCompareOutcomePresenter _presenter;

    public SpecCompareApplication(IAnsiConsole console)
        : this(
            new SpecCompareRequestResolver(),
            new SpecComparePipeline(
                new DocumentPipeline(),
                new ExcelReportWriter(),
                new SpecCompareDiagnosticsBuilder()),
            new ConsoleProgressRunner(console),
            new SpecCompareOutcomePresenter(console))
    {
    }

    internal SpecCompareApplication(
        SpecCompareRequestResolver requestResolver,
        SpecComparePipeline pipeline,
        ConsoleProgressRunner progressRunner,
        SpecCompareOutcomePresenter presenter)
    {
        _requestResolver = requestResolver ?? throw new ArgumentNullException(nameof(requestResolver));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _progressRunner = progressRunner ?? throw new ArgumentNullException(nameof(progressRunner));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public int Run(SpecCompareRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = _requestResolver.Resolve(request);
        if (!resolution.IsValid || resolution.Value is null)
        {
            _presenter.WriteValidationError(resolution.ErrorMessage);
            return resolution.ExitCode;
        }

        var resolvedRequest = resolution.Value;
        _presenter.WriteConfiguration(resolvedRequest);

        try
        {
            var runResult = _progressRunner.Run(
                progress => _pipeline.Execute(resolvedRequest, progress, cancellationToken),
                cancellationToken);
            _presenter.WriteSuccess(runResult);
            return 0;
        }
        catch (Exception exception)
        {
            return _presenter.WriteFailure(exception);
        }
    }
}
