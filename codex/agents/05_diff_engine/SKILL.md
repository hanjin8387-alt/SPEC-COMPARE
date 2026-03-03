---
name: diff-engine
description: >
  Phase 4a agent — computes paragraph-level diffs between matched chapter pairs
  using LCS/DiffPlex, classifies changes as Added/Deleted/Modified, and
  produces DiffItem lists with truncated text.
---

# Agent 05 — Diff Engine

## Scope

Compare matched chapter pairs at paragraph granularity, classify changes,
and compute similarity scores.

## Inputs

- `List<ChapterPair>` from Agent 04
- Similarity threshold (default 0.85)
- `codex/SKILL.md` for data contracts & constraints

## Outputs

- `Pipeline/DiffEngine.cs` — paragraph diff + classification
- `Helpers/SimilarityCalculator.cs` — Levenshtein distance normalized to [0,1]
- `List<DiffItem>` with truncated text (≤ 500 chars)
- Unit tests

## Acceptance Criteria

1. Paragraphs split on double-newline (`\n\n`) boundary
2. Changes classified correctly:
   - **Added**: paragraph in target only
   - **Deleted**: paragraph in source only
   - **Modified**: exists in both, similarity ≥ threshold
   - If similarity < threshold → treated as Deleted + Added (not Modified)
3. `DiffItem.BeforeText` and `AfterText` truncated to ≤ 500 characters
4. Similarity score is `double` in range [0.0, 1.0]
5. No document content stored beyond `DiffItem` truncated excerpts
6. `dotnet build --warnaserror` passes

## Detailed Instructions

### SimilarityCalculator.cs

```csharp
public static class SimilarityCalculator
{
    /// <summary>
    /// Returns Levenshtein similarity in [0.0, 1.0].
    /// 1.0 = identical, 0.0 = completely different.
    /// </summary>
    public static double Calculate(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - ((double)distance / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var cost = s[i - 1] == t[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost
            );
        }

        return d[n, m];
    }
}
```

### DiffEngine.cs

```csharp
public static class DiffEngine
{
    private const int TruncateLimit = 500;

    public static List<DiffItem> ComputeDiffs(
        List<ChapterPair> pairs,
        double similarityThreshold = 0.85)
    {
        var diffs = new List<DiffItem>();

        foreach (var pair in pairs)
        {
            if (pair.Source == null && pair.Target != null)
            {
                // Entire chapter added
                diffs.Add(CreateDiffItem(pair.Target.Key,
                    ChangeType.Added, "", pair.Target.Content.ToString(),
                    0.0, $"p{pair.Target.PageStart}-{pair.Target.PageEnd}"));
                continue;
            }

            if (pair.Source != null && pair.Target == null)
            {
                // Entire chapter deleted
                diffs.Add(CreateDiffItem(pair.Source.Key,
                    ChangeType.Deleted, pair.Source.Content.ToString(), "",
                    0.0, $"p{pair.Source.PageStart}-{pair.Source.PageEnd}"));
                continue;
            }

            // Both exist — compare paragraphs
            var sourceParagraphs = SplitParagraphs(pair.Source!.Content.ToString());
            var targetParagraphs = SplitParagraphs(pair.Target!.Content.ToString());

            var paragraphDiffs = ComputeParagraphDiffs(
                sourceParagraphs, targetParagraphs, similarityThreshold);

            foreach (var d in paragraphDiffs)
            {
                diffs.Add(CreateDiffItem(
                    pair.Source.Key, d.Type, d.Before, d.After,
                    d.Similarity,
                    $"p{pair.Source.PageStart}-{pair.Target.PageEnd}"));
            }
        }

        return diffs;
    }

    private static string[] SplitParagraphs(string text)
        => text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
               .Select(p => p.Trim())
               .Where(p => !string.IsNullOrWhiteSpace(p))
               .ToArray();

    private static string Truncate(string text)
        => text.Length <= TruncateLimit
            ? text
            : text[..TruncateLimit] + "…";

    private static DiffItem CreateDiffItem(
        string key, ChangeType type, string before, string after,
        double similarity, string pageRef)
        => new()
        {
            ChapterKey = key,
            ChangeType = type,
            BeforeText = Truncate(before),
            AfterText = Truncate(after),
            SimilarityScore = Math.Round(similarity, 4),
            PageRef = pageRef
        };

    // Use DiffPlex or custom LCS for paragraph alignment
    // ...
}
```

### Paragraph Alignment Algorithm

Use DiffPlex `Differ` for line-level alignment:
```csharp
var differ = new DiffPlex.Differ();
var result = differ.CreateDiffs(sourceText, targetText,
    ignoreWhiteSpace: true, ignoreCase: false, chunker: new LineChunker());
```

Or implement custom LCS matching on the paragraph arrays.

## Edge Cases

- **Identical chapters**: No diff items generated
- **Empty chapter content**: Treated as all-deleted or all-added respectively
- **Very long paragraphs (> 500 chars)**: Truncated in DiffItem, not in comparison
- **Single-word paragraph**: Still valid for comparison
- **Large paragraph count**: LCS can be O(n²) — consider early termination for > 1000 paragraphs
- **Unicode in similarity calculation**: Levenshtein works on char-by-char basis

## Never Do

- ❌ Store full paragraph text in DiffItem (must truncate to ≤ 500)
- ❌ Log any text content (before/after text, similarity calculations)
- ❌ Persist diff results to disk (except via Excel reporter)
- ❌ Use external AI/ML for classification

## Suggested Unit Tests

```csharp
[Fact]
public void SimilarityCalculator_IdenticalStrings_Returns1()
{
    Assert.Equal(1.0, SimilarityCalculator.Calculate("hello", "hello"));
}

[Fact]
public void SimilarityCalculator_CompletelyDifferent_ReturnsLow()
{
    var score = SimilarityCalculator.Calculate("abc", "xyz");
    Assert.True(score < 0.5);
}

[Fact]
public void SimilarityCalculator_SimilarStrings_ReturnsHigh()
{
    var score = SimilarityCalculator.Calculate(
        "The system shall process inputs",
        "The system shall process all inputs");
    Assert.True(score >= 0.8);
}

[Fact]
public void SimilarityCalculator_EmptyStrings_Returns1()
{
    Assert.Equal(1.0, SimilarityCalculator.Calculate("", ""));
}

[Fact]
public void SimilarityCalculator_OneEmpty_Returns0()
{
    Assert.Equal(0.0, SimilarityCalculator.Calculate("abc", ""));
}

[Fact]
public void DiffEngine_AddedChapter_ProducesAddedItem()
{
    var pairs = new List<ChapterPair>
    {
        new(null, new ChapterNode { Key = "3", Title = "New" })
    };
    var diffs = DiffEngine.ComputeDiffs(pairs);
    Assert.Single(diffs);
    Assert.Equal(ChangeType.Added, diffs[0].ChangeType);
}

[Fact]
public void DiffEngine_DeletedChapter_ProducesDeletedItem()
{
    var pairs = new List<ChapterPair>
    {
        new(new ChapterNode { Key = "2", Title = "Old" }, null)
    };
    var diffs = DiffEngine.ComputeDiffs(pairs);
    Assert.Single(diffs);
    Assert.Equal(ChangeType.Deleted, diffs[0].ChangeType);
}

[Fact]
public void DiffEngine_ModifiedParagraph_ClassifiedCorrectly()
{
    var source = new ChapterNode { Key = "1", Title = "Test" };
    source.Content.Append("Paragraph one.\n\nOriginal paragraph two.");

    var target = new ChapterNode { Key = "1", Title = "Test" };
    target.Content.Append("Paragraph one.\n\nModified paragraph two slightly.");

    var pairs = new List<ChapterPair> { new(source, target) };
    var diffs = DiffEngine.ComputeDiffs(pairs, 0.5);

    Assert.Contains(diffs, d => d.ChangeType == ChangeType.Modified);
}

[Fact]
public void DiffEngine_TruncatesLongText()
{
    var longText = new string('A', 1000);
    var source = new ChapterNode { Key = "1", Title = "Test" };
    source.Content.Append(longText);

    var pairs = new List<ChapterPair> { new(source, null) };
    var diffs = DiffEngine.ComputeDiffs(pairs);

    Assert.True(diffs[0].BeforeText.Length <= 501); // 500 + "…"
}
```
