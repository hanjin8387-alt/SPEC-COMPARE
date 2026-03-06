using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Helpers;

public static class PipelineConfigResolver
{
    public static ConfigLoadResult Load(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return ConfigLoadResult.Valid(new PipelineConfig());
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<PipelineConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    Converters =
                    {
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                    }
                });

            return ConfigLoadResult.Valid(config ?? new PipelineConfig());
        }
        catch (UnauthorizedAccessException)
        {
            return ConfigLoadResult.Invalid("Config file cannot be read.", isIoRelated: true);
        }
        catch (IOException)
        {
            return ConfigLoadResult.Invalid("Config file cannot be read.", isIoRelated: true);
        }
        catch (JsonException)
        {
            return ConfigLoadResult.Invalid("Config JSON is invalid.");
        }
    }

    public static ResolvedPipelineOptions Resolve(
        PipelineConfig? config,
        double? diffThresholdOverride,
        double? chapterMatchThresholdOverride,
        double defaultDiffThreshold,
        double defaultChapterMatchThreshold)
    {
        return Resolve(
            config,
            diffThresholdOverride,
            chapterMatchThresholdOverride,
            includeFullTextSheetOverride: null,
            previewTextLengthOverride: null,
            diagnosticsVerbosityOverride: null,
            defaultDiffThreshold,
            defaultChapterMatchThreshold);
    }

    public static ResolvedPipelineOptions Resolve(
        PipelineConfig? config,
        double? diffThresholdOverride,
        double? chapterMatchThresholdOverride,
        bool? includeFullTextSheetOverride,
        int? previewTextLengthOverride,
        DiagnosticsVerbosity? diagnosticsVerbosityOverride,
        double defaultDiffThreshold,
        double defaultChapterMatchThreshold)
    {
        config ??= new PipelineConfig();
        var reporting = config.Reporting ?? new ReportOptions();

        return new ResolvedPipelineOptions(
            diffThresholdOverride ?? config.DiffThreshold ?? defaultDiffThreshold,
            chapterMatchThresholdOverride ?? config.ChapterMatchThreshold ?? defaultChapterMatchThreshold,
            config.TextNormalization ?? new TextNormalizationOptions(),
            config.ChapterSegmentation ?? new ChapterSegmentationOptions(),
            new ReportOptions
            {
                IncludeFullTextSheet = includeFullTextSheetOverride ?? reporting.IncludeFullTextSheet,
                PreviewTextLength = previewTextLengthOverride ?? reporting.PreviewTextLength,
                DiagnosticsVerbosity = diagnosticsVerbosityOverride ?? reporting.DiagnosticsVerbosity
            });
    }
}

public readonly record struct ResolvedPipelineOptions(
    double DiffThreshold,
    double ChapterMatchThreshold,
    TextNormalizationOptions TextNormalization,
    ChapterSegmentationOptions ChapterSegmentation,
    ReportOptions Reporting);

public readonly record struct ConfigLoadResult(
    bool IsValid,
    PipelineConfig? Config,
    string? ErrorMessage,
    bool IsIoRelated)
{
    public static ConfigLoadResult Valid(PipelineConfig config)
    {
        return new ConfigLoadResult(true, config, null, false);
    }

    public static ConfigLoadResult Invalid(string errorMessage, bool isIoRelated = false)
    {
        return new ConfigLoadResult(false, null, errorMessage, isIoRelated);
    }
}
