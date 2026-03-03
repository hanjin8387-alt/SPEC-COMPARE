namespace PdfSpecDiffReporter.Models;

public sealed record ChapterPair(ChapterNode? Source, ChapterNode? Target, double TitleSimilarity);
