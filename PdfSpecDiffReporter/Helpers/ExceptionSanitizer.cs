using System;
using System.IO;

namespace PdfSpecDiffReporter.Helpers;

public static class ExceptionSanitizer
{
    public const int RuntimeExitCode = 1;
    public const int ValidationExitCode = 2;
    public const int IoExitCode = 3;
    public const int CanceledExitCode = 4;

    public static SanitizedExceptionInfo Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var classified = exception as ClassifiedException;
        var root = classified?.InnerException ?? exception;
        var correlationId = classified?.CorrelationId ?? CreateCorrelationId();
        var exitCode = classified?.ExitCode ?? DetermineExitCode(root);

        var message = root switch
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

        if (exception is ClassifiedException classifiedException)
        {
            return classifiedException;
        }

        return new ClassifiedException(
            CreateCorrelationId(),
            DetermineExitCode(exception),
            exception);
    }

    public static int DetermineExitCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is ClassifiedException classifiedException)
        {
            return classifiedException.ExitCode;
        }

        if (exception is OperationCanceledException)
        {
            return CanceledExitCode;
        }

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

        return exception.InnerException is not null && IsIoException(exception.InnerException);
    }

    private static bool IsValidationException(Exception exception)
    {
        if (exception is ArgumentException or FormatException)
        {
            return true;
        }

        return exception.InnerException is not null && IsValidationException(exception.InnerException);
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}

public readonly record struct SanitizedExceptionInfo(
    string Message,
    string CorrelationId,
    int ExitCode);

public sealed class ClassifiedException : InvalidOperationException
{
    public ClassifiedException(string correlationId, int exitCode, Exception innerException)
        : base("Operation failed.", innerException)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        ExitCode = exitCode;
    }

    public string CorrelationId { get; }

    public int ExitCode { get; }
}
