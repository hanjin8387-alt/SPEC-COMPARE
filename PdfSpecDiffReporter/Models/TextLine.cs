namespace PdfSpecDiffReporter.Models;

public sealed record TextLine(
    int PageNumber,
    string Text,
    string NormalizedText,
    double Y,
    double MinX,
    double MaxX,
    double MaxFontSize,
    double AverageFontSize,
    int WordCount);
