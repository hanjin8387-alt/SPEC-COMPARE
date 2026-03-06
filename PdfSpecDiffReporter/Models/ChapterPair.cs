namespace PdfSpecDiffReporter.Models;

public sealed record ChapterPair(ChapterNode? Source, ChapterNode? Target, ChapterMatchEvidence? Evidence);
