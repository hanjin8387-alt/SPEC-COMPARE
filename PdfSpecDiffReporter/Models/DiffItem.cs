namespace PdfSpecDiffReporter.Models;

public sealed record DiffItem(
    string ChapterKey,
    ChangeType ChangeType,
    string BeforeText,
    string AfterText,
    double SimilarityScore,
    string PageRef);
