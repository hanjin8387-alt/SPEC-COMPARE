using System;
using System.IO;

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

    public static ValidationResult ValidateThreshold(double threshold)
    {
        if (double.IsNaN(threshold) || double.IsInfinity(threshold) || threshold <= 0d || threshold > 1d)
        {
            return ValidationResult.Invalid("Threshold must be greater than 0 and less than or equal to 1.");
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
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

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
