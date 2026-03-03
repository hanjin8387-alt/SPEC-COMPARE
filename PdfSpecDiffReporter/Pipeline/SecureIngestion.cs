using System;
using System.IO;
using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Pipeline;

public static class SecureIngestion
{
    public static MemoryStream LoadToMemory(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be null or whitespace.", nameof(filePath));
        }

        var memoryStream = new MemoryStream();

        try
        {
            using (var fileStream = new FileStream(
                       filePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                fileStream.CopyTo(memoryStream);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            memoryStream.Dispose();
            throw ExceptionSanitizer.Wrap(ex);
        }
    }
}
