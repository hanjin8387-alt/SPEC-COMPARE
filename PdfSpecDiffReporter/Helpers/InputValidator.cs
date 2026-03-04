using System;
using System.IO;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Helpers;

public static class InputValidator
{
    public static ValidationResult ValidatePdfPath(string? path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid($"{displayName} path is required.");
        }

        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"{displayName} must use .pdf extension.");
        }

        if (!File.Exists(path))
        {
            return ValidationResult.Invalid($"{displayName} was not found.");
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 5)
            {
                return ValidationResult.Invalid($"{displayName} is not a valid PDF file.");
            }

            Span<byte> header = stackalloc byte[5];
            var readCount = stream.Read(header);
            if (readCount < 5 ||
                header[0] != (byte)'%' ||
                header[1] != (byte)'P' ||
                header[2] != (byte)'D' ||
                header[3] != (byte)'F' ||
                header[4] != (byte)'-')
            {
                return ValidationResult.Invalid($"{displayName} is not a valid PDF file.");
            }

            return ValidationResult.Valid();
        }
        catch (UnauthorizedAccessException)
        {
            return ValidationResult.Invalid($"{displayName} cannot be read.", isIoRelated: true);
        }
        catch (IOException)
        {
            return ValidationResult.Invalid($"{displayName} cannot be read.", isIoRelated: true);
        }
    }

    public static ValidationResult ValidateOptionalConfigPath(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return ValidationResult.Valid();
        }

        if (!configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Config file must use .json extension.");
        }

        if (!File.Exists(configPath))
        {
            return ValidationResult.Invalid("Config file was not found.");
        }

        try
        {
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ValidationResult.Valid();
        }
        catch (UnauthorizedAccessException)
        {
            return ValidationResult.Invalid("Config file cannot be read.", isIoRelated: true);
        }
        catch (IOException)
        {
            return ValidationResult.Invalid("Config file cannot be read.", isIoRelated: true);
        }
    }

    public static ValidationResult ValidateSimilarityThreshold(double threshold, string displayName)
    {
        if (double.IsNaN(threshold) || double.IsInfinity(threshold) || threshold <= 0d || threshold > 1d)
        {
            return ValidationResult.Invalid($"{displayName} must be greater than 0 and less than or equal to 1.");
        }

        return ValidationResult.Valid();
    }

    public static ValidationResult ValidateTextNormalizationOptions(TextNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.HeaderFooterBandPercent <= 0d || options.HeaderFooterBandPercent >= 0.5d)
        {
            return ValidationResult.Invalid("textNormalization.headerFooterBandPercent must be between 0 and 0.5.");
        }

        if (options.MinRepeatingPages < 2 || options.MinRepeatingPages > 20)
        {
            return ValidationResult.Invalid("textNormalization.minRepeatingPages must be between 2 and 20.");
        }

        if (options.RepeatingSimilarityThreshold <= 0d || options.RepeatingSimilarityThreshold > 1d)
        {
            return ValidationResult.Invalid("textNormalization.repeatingSimilarityThreshold must be within (0, 1].");
        }

        if (options.LineMergeTolerance <= 0d || options.LineMergeTolerance > 10d)
        {
            return ValidationResult.Invalid("textNormalization.lineMergeTolerance must be within (0, 10].");
        }

        if (options.ZoneLineLimit < 1 || options.ZoneLineLimit > 10)
        {
            return ValidationResult.Invalid("textNormalization.zoneLineLimit must be between 1 and 10.");
        }

        if (options.SearchWindow < 1 || options.SearchWindow > 30)
        {
            return ValidationResult.Invalid("textNormalization.searchWindow must be between 1 and 30.");
        }

        if (options.MinZoneTextLength < 0 || options.MinZoneTextLength > 100)
        {
            return ValidationResult.Invalid("textNormalization.minZoneTextLength must be between 0 and 100.");
        }

        return ValidationResult.Valid();
    }

    public static ValidationResult ValidateChapterSegmentationOptions(ChapterSegmentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TocScanPageCount < 1 || options.TocScanPageCount > 50)
        {
            return ValidationResult.Invalid("chapterSegmentation.tocScanPageCount must be between 1 and 50.");
        }

        if (options.LayoutHeadingFontRatio < 1d || options.LayoutHeadingFontRatio > 2.5d)
        {
            return ValidationResult.Invalid("chapterSegmentation.layoutHeadingFontRatio must be between 1 and 2.5.");
        }

        if (options.MinHeadingScore <= 0d || options.MinHeadingScore > 1d)
        {
            return ValidationResult.Invalid("chapterSegmentation.minHeadingScore must be within (0, 1].");
        }

        if (options.MaxHeadingWords < 3 || options.MaxHeadingWords > 40)
        {
            return ValidationResult.Invalid("chapterSegmentation.maxHeadingWords must be between 3 and 40.");
        }

        if (options.MaxHeadingLength < 10 || options.MaxHeadingLength > 400)
        {
            return ValidationResult.Invalid("chapterSegmentation.maxHeadingLength must be between 10 and 400.");
        }

        return ValidationResult.Valid();
    }

    public static OutputPathValidationResult ValidateOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OutputPathValidationResult.Invalid("Output path is required.");
        }

        var expandedPath = outputPath.Replace("{Timestamp}", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"), StringComparison.Ordinal);
        if (!expandedPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OutputPathValidationResult.Invalid("Output file must use .xlsx extension.");
        }

        try
        {
            var fullPath = Path.GetFullPath(expandedPath);
            return OutputPathValidationResult.Valid(fullPath);
        }
        catch (UnauthorizedAccessException)
        {
            return OutputPathValidationResult.Invalid("Output directory cannot be accessed.", isIoRelated: true);
        }
        catch (PathTooLongException)
        {
            return OutputPathValidationResult.Invalid("Output path is too long.", isIoRelated: true);
        }
        catch (IOException)
        {
            return OutputPathValidationResult.Invalid("Output path cannot be used.", isIoRelated: true);
        }
        catch (ArgumentException)
        {
            return OutputPathValidationResult.Invalid("Output path is not valid.");
        }
        catch (NotSupportedException)
        {
            return OutputPathValidationResult.Invalid("Output path format is not supported.");
        }
    }
}

public readonly record struct ValidationResult(
    bool IsValid,
    string? ErrorMessage,
    bool IsIoRelated)
{
    public static ValidationResult Valid()
    {
        return new ValidationResult(true, null, false);
    }

    public static ValidationResult Invalid(string errorMessage, bool isIoRelated = false)
    {
        return new ValidationResult(false, errorMessage, isIoRelated);
    }
}

public readonly record struct OutputPathValidationResult(
    bool IsValid,
    string? ResolvedPath,
    string? ErrorMessage,
    bool IsIoRelated)
{
    public static OutputPathValidationResult Valid(string resolvedPath)
    {
        return new OutputPathValidationResult(true, resolvedPath, null, false);
    }

    public static OutputPathValidationResult Invalid(string errorMessage, bool isIoRelated = false)
    {
        return new OutputPathValidationResult(false, null, errorMessage, isIoRelated);
    }
}
