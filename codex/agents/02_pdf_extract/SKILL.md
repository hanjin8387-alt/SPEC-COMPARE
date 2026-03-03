---
name: pdf-extract
description: >
  Phase 1 agent — implements secure in-memory PDF loading and per-page
  text extraction using PdfPig. FileStream must be closed immediately.
---

# Agent 02 — PDF Text Extraction

## Scope

Implement secure PDF ingestion (file → memory → close handle) and
page-level text extraction with word coordinates.

## Inputs

- File path to a PDF (validated by CLI layer)
- `codex/SKILL.md` for memory policy & constraints

## Outputs

- `Pipeline/SecureIngestion.cs` — loads PDF into `MemoryStream`
- `Models/PageText.cs` — page-level text data
- Updated tests in `PdfSpecDiffReporter.Tests/`

## Acceptance Criteria

1. After `SecureIngestion.Load(path)` returns, the source `FileStream` is **closed and disposed**
2. The returned `MemoryStream` contains the full PDF bytes
3. Text extraction produces one `PageText` per page with:
   - `PageNumber` (1-based)
   - `RawText` (full page text)
   - `Words` list with `(Text, X, Y, FontSize)` tuples
4. No full-document text is ever stored in a single `string`
5. `dotnet build --warnaserror` passes

## Detailed Instructions

### SecureIngestion.cs

```csharp
public static class SecureIngestion
{
    public static MemoryStream LoadToMemory(string filePath)
    {
        var memoryStream = new MemoryStream();
        using (var fileStream = new FileStream(filePath, FileMode.Open,
            FileAccess.Read, FileShare.None))
        {
            fileStream.CopyTo(memoryStream);
        }
        // FileStream is now CLOSED — file handle released
        memoryStream.Position = 0;
        return memoryStream;
    }
}
```

### PageText Model

```csharp
public sealed record PageText(
    int PageNumber,
    string RawText,
    IReadOnlyList<WordInfo> Words
);

public sealed record WordInfo(
    string Text,
    double X,
    double Y,
    double FontSize
);
```

### Text Extraction (PdfPig)

```csharp
public static class PdfTextExtractor
{
    public static List<PageText> ExtractPages(MemoryStream pdfStream)
    {
        var pages = new List<PageText>();
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords()
                .Select(w => new WordInfo(
                    w.Text,
                    w.BoundingBox.Left,
                    w.BoundingBox.Bottom,
                    w.Letters.FirstOrDefault()?.PointSize ?? 0
                ))
                .ToList();

            pages.Add(new PageText(
                page.Number,
                page.Text,
                words
            ));
        }
        return pages;
    }
}
```

## Edge Cases

- **Corrupt/encrypted PDF:** PdfPig throws `InvalidOperationException` — catch and wrap with `ExceptionSanitizer`
- **Empty PDF (0 pages):** Return empty list, do NOT throw
- **Very large PDF (500+ pages):** Process sequentially to avoid memory spike; consider `yield return` if needed
- **PDF with images only (no text):** Return pages with empty `RawText` — downstream will handle
- **File locked by another process:** `FileShare.None` will throw `IOException` — catch and provide clear error

## Never Do

- ❌ Keep `FileStream` open after copying to memory
- ❌ Store all page texts concatenated into one giant string
- ❌ Log any extracted text content to console or file
- ❌ Write extracted text to disk (temp files, cache, etc.)
- ❌ Call any network API

## Suggested Unit Tests

```csharp
[Fact]
public void LoadToMemory_ClosesFileStream()
{
    // Create a temp file, load it, then verify file is unlocked
    var tempPath = Path.GetTempFileName();
    File.WriteAllText(tempPath, "dummy");
    try
    {
        using var ms = SecureIngestion.LoadToMemory(tempPath);
        // If FileStream is still open, this would throw
        using var verify = new FileStream(tempPath,
            FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(verify.CanRead);
    }
    finally { File.Delete(tempPath); }
}

[Fact]
public void LoadToMemory_ReturnsNonEmptyStream()
{
    var tempPath = Path.GetTempFileName();
    File.WriteAllBytes(tempPath, new byte[] { 1, 2, 3 });
    try
    {
        using var ms = SecureIngestion.LoadToMemory(tempPath);
        Assert.Equal(3, ms.Length);
        Assert.Equal(0, ms.Position);
    }
    finally { File.Delete(tempPath); }
}

[Fact]
public void ExtractPages_EmptyStream_ReturnsEmptyList()
{
    // Test with a minimal valid PDF (single empty page)
    // or expect a handled exception for truly empty stream
}
```
