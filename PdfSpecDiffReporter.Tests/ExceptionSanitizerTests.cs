using System;
using System.IO;
using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Tests;

public sealed class ExceptionSanitizerTests
{
    [Fact]
    public void DetermineExitCode_ClassifiesIoAndValidationExceptions()
    {
        Assert.Equal(ExceptionSanitizer.IoExitCode, ExceptionSanitizer.DetermineExitCode(new IOException("disk error")));
        Assert.Equal(ExceptionSanitizer.ValidationExitCode, ExceptionSanitizer.DetermineExitCode(new ArgumentOutOfRangeException("threshold")));
        Assert.Equal(ExceptionSanitizer.CanceledExitCode, ExceptionSanitizer.DetermineExitCode(new OperationCanceledException()));
    }

    [Fact]
    public void DetermineExitCode_UsesInnerExceptionForWrappedErrors()
    {
        var wrapped = ExceptionSanitizer.Wrap(new IOException("C:\\secret\\spec.pdf"));

        Assert.Equal(ExceptionSanitizer.IoExitCode, ExceptionSanitizer.DetermineExitCode(wrapped));
    }

    [Fact]
    public void Sanitize_HidesSensitiveExceptionMessage()
    {
        var sanitized = ExceptionSanitizer.Sanitize(new IOException("C:\\secret\\spec.pdf"));

        Assert.StartsWith("File access error.", sanitized.Message);
        Assert.Contains("[Ref: ", sanitized.Message);
        Assert.DoesNotContain("secret", sanitized.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrapAndSanitize_PreserveCorrelationId()
    {
        var wrapped = Assert.IsType<ClassifiedException>(ExceptionSanitizer.Wrap(new IOException("C:\\secret\\spec.pdf")));

        var sanitized = ExceptionSanitizer.Sanitize(wrapped);

        Assert.Equal(wrapped.CorrelationId, sanitized.CorrelationId);
        Assert.Contains(wrapped.CorrelationId, sanitized.Message, StringComparison.Ordinal);
    }
}
