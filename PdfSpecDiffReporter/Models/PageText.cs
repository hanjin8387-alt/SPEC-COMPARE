using System.Collections.Generic;

namespace PdfSpecDiffReporter.Models;

public sealed record PageText(
    int PageNumber,
    string RawText,
    IReadOnlyList<WordInfo> Words);
