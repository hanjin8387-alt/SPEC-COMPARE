using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Application;

public sealed class SpecCompareRequestResolver
{
    private const double DefaultDiffThreshold = 0.85d;
    private const double DefaultChapterMatchThreshold = 0.70d;

    public RequestResolutionResult Resolve(SpecCompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceValidation = InputValidator.ValidatePdfPath(request.SourcePdfPath, "Source PDF");
        if (!sourceValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(sourceValidation.ErrorMessage, sourceValidation.IsIoRelated);
        }

        var targetValidation = InputValidator.ValidatePdfPath(request.TargetPdfPath, "Target PDF");
        if (!targetValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(targetValidation.ErrorMessage, targetValidation.IsIoRelated);
        }

        var configValidation = InputValidator.ValidateOptionalConfigPath(request.ConfigPath);
        if (!configValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(configValidation.ErrorMessage, configValidation.IsIoRelated);
        }

        var outputValidation = InputValidator.ValidateOutputPath(request.OutputPath);
        if (!outputValidation.IsValid || string.IsNullOrWhiteSpace(outputValidation.ResolvedPath))
        {
            return RequestResolutionResult.Invalid(outputValidation.ErrorMessage, outputValidation.IsIoRelated);
        }

        var configLoadResult = PipelineConfigResolver.Load(request.ConfigPath);
        if (!configLoadResult.IsValid || configLoadResult.Config is null)
        {
            return RequestResolutionResult.Invalid(configLoadResult.ErrorMessage, configLoadResult.IsIoRelated);
        }

        var resolvedOptions = PipelineConfigResolver.Resolve(
            configLoadResult.Config,
            request.DiffThresholdOverride,
            request.ChapterMatchThresholdOverride,
            request.IncludeFullTextSheetOverride,
            request.PreviewTextLengthOverride,
            request.DiagnosticsVerbosityOverride,
            DefaultDiffThreshold,
            DefaultChapterMatchThreshold);

        var diffThresholdValidation = InputValidator.ValidateSimilarityThreshold(resolvedOptions.DiffThreshold, "Diff threshold");
        if (!diffThresholdValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(diffThresholdValidation.ErrorMessage);
        }

        var chapterMatchThresholdValidation = InputValidator.ValidateSimilarityThreshold(
            resolvedOptions.ChapterMatchThreshold,
            "Chapter match threshold");
        if (!chapterMatchThresholdValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(chapterMatchThresholdValidation.ErrorMessage);
        }

        var normalizationValidation = InputValidator.ValidateTextNormalizationOptions(resolvedOptions.TextNormalization);
        if (!normalizationValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(normalizationValidation.ErrorMessage);
        }

        var segmentationValidation = InputValidator.ValidateChapterSegmentationOptions(resolvedOptions.ChapterSegmentation);
        if (!segmentationValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(segmentationValidation.ErrorMessage);
        }

        var reportValidation = InputValidator.ValidateReportOptions(resolvedOptions.Reporting);
        if (!reportValidation.IsValid)
        {
            return RequestResolutionResult.Invalid(reportValidation.ErrorMessage);
        }

        return RequestResolutionResult.Valid(
            new ResolvedSpecCompareRequest(
                request.SourcePdfPath,
                request.TargetPdfPath,
                outputValidation.ResolvedPath,
                request.ConfigPath,
                resolvedOptions));
    }
}

public readonly record struct RequestResolutionResult(
    bool IsValid,
    ResolvedSpecCompareRequest? Value,
    string? ErrorMessage,
    int ExitCode)
{
    public static RequestResolutionResult Valid(ResolvedSpecCompareRequest value)
    {
        return new RequestResolutionResult(true, value, null, 0);
    }

    public static RequestResolutionResult Invalid(string? errorMessage, bool isIoRelated = false)
    {
        return new RequestResolutionResult(
            false,
            null,
            errorMessage,
            isIoRelated ? ExceptionSanitizer.IoExitCode : ExceptionSanitizer.ValidationExitCode);
    }
}
