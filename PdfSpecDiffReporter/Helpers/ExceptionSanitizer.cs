using System;
using System.IO;

namespace PdfSpecDiffReporter.Helpers;

public static class ExceptionSanitizer
{
    public const int RuntimeExitCode = 1;
    public const int ValidationExitCode = 2;
    public const int IoExitCode = 3;

    private static readonly string[] IoMarkers =
    {
        "[IOException]",
        "[UnauthorizedAccessException]",
        "[FileNotFoundException]",
        "[DirectoryNotFoundException]",
        "[PathTooLongException]"
    };

    private static readonly string[] ValidationMarkers =
    {
        "[ArgumentException]",
        "[FormatException]",
        "[ArgumentOutOfRangeException]",
        "[ArgumentNullException]"
    };

    public static SanitizedExceptionInfo Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var exitCode = DetermineExitCode(exception);

        var message = exception switch
        {
            OperationCanceledException => "Operation canceled by user.",
            OutOfMemoryException => "Insufficient memory.",
            ArgumentException => "Invalid argument.",
            _ when exitCode == IoExitCode => "File access error.",
            _ when exitCode == ValidationExitCode => "Invalid argument.",
            _ => $"Unexpected runtime error ({exception.GetType().Name})."
        };

        return new SanitizedExceptionInfo(
            $"{message} [Ref: {correlationId}]",
            correlationId,
            exitCode);
    }

    public static InvalidOperationException Wrap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        return new InvalidOperationException(
            $"Operation failed. Correlation={correlationId}",
            exception);
    }

    public static int DetermineExitCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsIoException(exception))
        {
            return IoExitCode;
        }

        if (IsValidationException(exception))
        {
            return ValidationExitCode;
        }

        return RuntimeExitCode;
    }

    private static bool IsIoException(Exception exception)
    {
        if (exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException or PathTooLongException)
        {
            return true;
        }

        if (exception is InvalidOperationException && ContainsAnyMarker(exception.Message, IoMarkers))
        {
            return true;
        }

        return exception.InnerException is not null && IsIoException(exception.InnerException);
    }

    private static bool IsValidationException(Exception exception)
    {
        if (exception is ArgumentException or FormatException)
        {
            return true;
        }

        if (exception is InvalidOperationException && ContainsAnyMarker(exception.Message, ValidationMarkers))
        {
            return true;
        }

        return exception.InnerException is not null && IsValidationException(exception.InnerException);
    }

    private static bool ContainsAnyMarker(string? message, IReadOnlyList<string> markers)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var marker in markers)
        {
            if (message.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public readonly record struct SanitizedExceptionInfo(
    string Message,
    string CorrelationId,
    int ExitCode);
