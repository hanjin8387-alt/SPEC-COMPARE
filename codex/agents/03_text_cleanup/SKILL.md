---
name: text-cleanup
description: >
  Phase 2 agent — removes repetitive headers/footers from extracted pages
  and normalizes text (whitespace, line endings, Unicode).
---

# Agent 03 — Text Cleanup & Normalization

## Scope

Clean extracted page texts by detecting and removing repeated headers/footers,
page numbers, and normalizing whitespace/encoding for consistent downstream processing.

## Inputs

- `List<PageText>` from Agent 02
- `codex/SKILL.md` for constraints

## Outputs

- `Pipeline/TextCleanup.cs` — header/footer detector + remover
- `Helpers/TextNormalizer.cs` — whitespace & Unicode normalizer
- Unit tests

## Acceptance Criteria

1. Headers/footers appearing on ≥ 3 consecutive pages with ≥ 0.9 similarity are removed
2. Page numbers (standalone numeric lines at top/bottom) are removed
3. Body text is preserved without alteration
4. Normalized text uses single spaces, `\n` line endings, NFC Unicode
5. Processing is page-by-page (no full-document string)
6. `dotnet build --warnaserror` passes

## Detailed Instructions

### Header/Footer Detection Algorithm

1. For each page, extract the **top 10%** and **bottom 10%** of text by Y-coordinate
2. Group these text blocks across consecutive pages
3. If the same text block (fuzzy similarity ≥ 0.9) appears on **≥ 3 consecutive pages**, mark it as header/footer
4. Remove all marked text blocks from all pages

```csharp
public static class TextCleanup
{
    public static List<PageText> RemoveHeadersFooters(
        List<PageText> pages,
        double yThresholdPercent = 0.10,
        int minConsecutivePages = 3,
        double similarityThreshold = 0.90)
    {
        // 1. Collect candidate lines from top/bottom zones
        // 2. Find lines repeating across consecutive pages
        // 3. Remove those lines from each page
        // 4. Return cleaned pages
    }
}
```

### Text Normalization

```csharp
public static class TextNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // 1. Normalize Unicode to NFC
        var result = input.Normalize(NormalizationForm.FormC);

        // 2. Remove control characters except \n and \t
        result = Regex.Replace(result, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // 3. Normalize line endings (\r\n, \r → \n)
        result = result.Replace("\r\n", "\n").Replace("\r", "\n");

        // 4. Collapse multiple spaces/tabs to single space (per line)
        result = string.Join("\n",
            result.Split('\n')
                .Select(line => Regex.Replace(line.Trim(), @"[ \t]+", " "))
        );

        // 5. Collapse excessive blank lines (3+ → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result.Trim();
    }
}
```

## Edge Cases

- **All pages have identical content** (e.g., all-watermark pages): Don't strip everything; require `minConsecutivePages` check
- **Header spans multiple lines** (company name + doc title + date): Compare multi-line blocks, not single lines
- **No headers/footers detected**: Return pages unchanged — do not error
- **Single-page document**: Skip header/footer detection entirely (< 3 pages)
- **Unicode combining characters**: NFC normalization handles these
- **Mixed encodings within one PDF**: PdfPig handles this; normalize after extraction
- **Page numbers in various formats**: "1", "Page 1", "1 of 50", "- 1 -" — detect via regex

## Never Do

- ❌ Store cleaned text to disk
- ❌ Log any page content (before or after cleaning)
- ❌ Modify text semantics — only remove noise and normalize formatting
- ❌ Use network for any detection logic

## Suggested Unit Tests

```csharp
[Fact]
public void RemoveHeadersFooters_DetectsRepeatedHeader()
{
    var pages = Enumerable.Range(1, 5).Select(i => new PageText(
        i,
        $"ACME Corp Confidential\n\nBody content for page {i}\n\nPage {i}",
        Array.Empty<WordInfo>()
    )).ToList();

    var cleaned = TextCleanup.RemoveHeadersFooters(pages);

    foreach (var page in cleaned)
    {
        Assert.DoesNotContain("ACME Corp Confidential", page.RawText);
        Assert.Contains("Body content", page.RawText);
    }
}

[Fact]
public void RemoveHeadersFooters_PreservesBodyWhenNoRepeats()
{
    var pages = new List<PageText>
    {
        new(1, "Unique header 1\nBody A\nFooter 1", Array.Empty<WordInfo>()),
        new(2, "Unique header 2\nBody B\nFooter 2", Array.Empty<WordInfo>()),
    };

    var cleaned = TextCleanup.RemoveHeadersFooters(pages);
    Assert.Equal("Unique header 1\nBody A\nFooter 1", cleaned[0].RawText);
}

[Fact]
public void Normalize_CollapsesWhitespace()
{
    var input = "Hello   world\t\t  test";
    var result = TextNormalizer.Normalize(input);
    Assert.Equal("Hello world test", result);
}

[Fact]
public void Normalize_FixesLineEndings()
{
    var input = "Line1\r\nLine2\rLine3\nLine4";
    var result = TextNormalizer.Normalize(input);
    Assert.Equal("Line1\nLine2\nLine3\nLine4", result);
}

[Fact]
public void Normalize_RemovesControlCharacters()
{
    var input = "Hello\x00\x01World";
    var result = TextNormalizer.Normalize(input);
    Assert.Equal("HelloWorld", result);
}

[Fact]
public void Normalize_EmptyInput_ReturnsEmpty()
{
    Assert.Equal("", TextNormalizer.Normalize(""));
    Assert.Equal("", TextNormalizer.Normalize(null));
}
```
