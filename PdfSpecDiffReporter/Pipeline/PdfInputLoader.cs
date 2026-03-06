using System;
using System.IO;
using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Pipeline;

public static class PdfInputLoader
{
    public static FileStream OpenRead(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be null or whitespace.", nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw ExceptionSanitizer.Wrap(ex);
        }
    }
}
