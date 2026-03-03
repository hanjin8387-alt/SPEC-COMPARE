---
name: chapter-split-match
description: >
  Phase 3 agent — segments cleaned text into ChapterNode trees and matches
  chapters between source and target PDFs using key + title similarity.
---

# Agent 04 — Chapter Segmentation & Matching

## Scope

Build a tree of `ChapterNode` objects from flat cleaned text (per PDF),
then align/match chapters between the two PDFs for diff comparison.

## Inputs

- Cleaned `List<PageText>` from Agent 03 (for each PDF)
- Chapter detection config from SPEC_PACK (regex pattern, strategy)
- `codex/SKILL.md` for data contracts

## Outputs

- `Pipeline/ChapterSegmenter.cs` — text → `List<ChapterNode>` tree
- `Pipeline/ChapterMatcher.cs` — align source & target chapters
- Unit tests

## Acceptance Criteria

1. Chapters detected by regex `^(\d+(\.\d+)*)\s+` or `^SECTION\s+\d+` or `^CHAPTER\s+\d+`
2. Hierarchy built correctly: "1.2.3" is child of "1.2", which is child of "1"
3. Each `ChapterNode` has correct `PageStart`, `PageEnd`, and accumulated `Content`
4. Matching produces:
   - Exact key matches ("1.2" ↔ "1.2")
   - Fuzzy title matches (Levenshtein ratio ≥ 0.8 for renamed chapters)
   - Unmatched items for added/deleted chapters
5. Document with no chapters → single root node containing all text
6. `dotnet build --warnaserror` passes

## Detailed Instructions

### ChapterSegmenter.cs

```csharp
public static class ChapterSegmenter
{
    private static readonly Regex ChapterPattern = new(
        @"^(\d+(?:\.\d+)*)\s+(.+)$|^(?:SECTION|CHAPTER)\s+(\d+)\s*[:\-]?\s*(.*)$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase
    );

    public static List<ChapterNode> Segment(List<PageText> pages)
    {
        var root = new List<ChapterNode>();
        var stack = new Stack<ChapterNode>();
        ChapterNode? current = null;

        foreach (var page in pages)
        {
            var lines = page.RawText.Split('\n');
            foreach (var line in lines)
            {
                var match = ChapterPattern.Match(line.Trim());
                if (match.Success)
                {
                    var key = match.Groups[1].Success
                        ? match.Groups[1].Value   // e.g., "1.2.3"
                        : match.Groups[3].Value;   // e.g., "3" from CHAPTER 3
                    var title = match.Groups[2].Success
                        ? match.Groups[2].Value.Trim()
                        : match.Groups[4].Value.Trim();

                    var node = new ChapterNode
                    {
                        Key = key,
                        Title = title,
                        PageStart = page.PageNumber
                    };

                    // Determine parent via depth
                    var depth = key.Split('.').Length;
                    while (stack.Count >= depth)
                        stack.Pop();

                    if (stack.Count > 0)
                        stack.Peek().Children.Add(node);
                    else
                        root.Add(node);

                    if (current != null)
                        current.PageEnd = page.PageNumber;

                    stack.Push(node);
                    current = node;
                }
                else if (current != null)
                {
                    current.Content.AppendLine(line);
                }
                else
                {
                    // Text before first chapter — create implicit root
                    current = new ChapterNode
                    {
                        Key = "0",
                        Title = "(Preamble)",
                        PageStart = page.PageNumber
                    };
                    current.Content.AppendLine(line);
                    root.Insert(0, current);
                }
            }
        }

        if (current != null)
            current.PageEnd = pages.LastOrDefault()?.PageNumber ?? 0;

        return root;
    }
}
```

### ChapterMatcher.cs

```csharp
public static class ChapterMatcher
{
    public static List<ChapterPair> Match(
        List<ChapterNode> source,
        List<ChapterNode> target,
        double titleSimilarityThreshold = 0.8)
    {
        var pairs = new List<ChapterPair>();
        var flatSource = Flatten(source);
        var flatTarget = Flatten(target);
        var matchedTargets = new HashSet<string>();

        // Pass 1: Exact key match
        foreach (var s in flatSource)
        {
            var t = flatTarget.FirstOrDefault(t =>
                t.Key == s.Key && !matchedTargets.Contains(t.Key));
            if (t != null)
            {
                pairs.Add(new ChapterPair(s, t));
                matchedTargets.Add(t.Key);
            }
        }

        // Pass 2: Fuzzy title match for unmatched
        var unmatchedSource = flatSource
            .Where(s => !pairs.Any(p => p.Source?.Key == s.Key));
        var unmatchedTarget = flatTarget
            .Where(t => !matchedTargets.Contains(t.Key)).ToList();

        foreach (var s in unmatchedSource)
        {
            var best = unmatchedTarget
                .Select(t => (Node: t, Score: SimilarityCalculator.Calculate(s.Title, t.Title)))
                .Where(x => x.Score >= titleSimilarityThreshold)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (best.Node != null)
            {
                pairs.Add(new ChapterPair(s, best.Node));
                unmatchedTarget.Remove(best.Node);
            }
            else
            {
                pairs.Add(new ChapterPair(s, null)); // Deleted chapter
            }
        }

        // Remaining unmatched targets → Added chapters
        foreach (var t in unmatchedTarget)
            pairs.Add(new ChapterPair(null, t));

        return pairs;
    }

    private static List<ChapterNode> Flatten(List<ChapterNode> nodes)
    {
        var result = new List<ChapterNode>();
        foreach (var node in nodes)
        {
            result.Add(node);
            result.AddRange(Flatten(node.Children));
        }
        return result;
    }
}

public sealed record ChapterPair(ChapterNode? Source, ChapterNode? Target);
```

## Edge Cases

- **No chapters detected**: Create single root node `Key="0"` with all content
- **Duplicate chapter keys**: Append suffix (e.g., "1.2_dup1") — log warning (no content!)
- **Deeply nested chapters** (e.g., "1.2.3.4.5"): Stack handles arbitrary depth
- **Chapter with no content**: Valid — it just has an empty `Content` StringBuilder
- **Both PDFs have completely different structures**: All chapters appear as Added + Deleted
- **Renumbered chapters** (same title, different key): Fuzzy match catches via title
- **TOC page detected as chapter**: Filter out single-line entries below min content threshold

## Never Do

- ❌ Log chapter titles or content to console/file
- ❌ Persist chapter tree to disk
- ❌ Use network for similarity calculation
- ❌ Modify the original `PageText` data

## Suggested Unit Tests

```csharp
[Fact]
public void Segment_ParsesNumberedChapters()
{
    var pages = new List<PageText>
    {
        new(1, "1 Introduction\nIntro text here.\n2 Background\nBackground text.", Array.Empty<WordInfo>()),
        new(2, "2.1 History\nHistorical context.\n3 Methods\nMethod description.", Array.Empty<WordInfo>())
    };

    var chapters = ChapterSegmenter.Segment(pages);

    Assert.Contains(chapters, c => c.Key == "1" && c.Title == "Introduction");
    Assert.Contains(chapters, c => c.Key == "2" && c.Title == "Background");
    Assert.Contains(chapters, c => c.Key == "3" && c.Title == "Methods");
}

[Fact]
public void Segment_BuildsHierarchy()
{
    var pages = new List<PageText>
    {
        new(1, "1 Parent\nParent text.\n1.1 Child\nChild text.\n1.2 Child2\nChild2 text.", Array.Empty<WordInfo>())
    };

    var chapters = ChapterSegmenter.Segment(pages);
    var parent = chapters.First(c => c.Key == "1");

    Assert.Equal(2, parent.Children.Count);
    Assert.Equal("1.1", parent.Children[0].Key);
    Assert.Equal("1.2", parent.Children[1].Key);
}

[Fact]
public void Segment_NoChapters_ReturnsSingleRoot()
{
    var pages = new List<PageText>
    {
        new(1, "Just some text without any chapter headings.", Array.Empty<WordInfo>())
    };

    var chapters = ChapterSegmenter.Segment(pages);
    Assert.Single(chapters);
    Assert.Equal("0", chapters[0].Key);
}

[Fact]
public void Match_ExactKeyMatch()
{
    var source = new List<ChapterNode>
    {
        new() { Key = "1", Title = "Introduction" },
        new() { Key = "2", Title = "Methods" }
    };
    var target = new List<ChapterNode>
    {
        new() { Key = "1", Title = "Introduction" },
        new() { Key = "2", Title = "Methods" }
    };

    var pairs = ChapterMatcher.Match(source, target);
    Assert.Equal(2, pairs.Count);
    Assert.All(pairs, p =>
    {
        Assert.NotNull(p.Source);
        Assert.NotNull(p.Target);
    });
}

[Fact]
public void Match_DetectsAddedAndDeletedChapters()
{
    var source = new List<ChapterNode>
    {
        new() { Key = "1", Title = "Old Chapter" }
    };
    var target = new List<ChapterNode>
    {
        new() { Key = "2", Title = "New Chapter" }
    };

    var pairs = ChapterMatcher.Match(source, target);
    var deleted = pairs.Where(p => p.Target == null).ToList();
    var added = pairs.Where(p => p.Source == null).ToList();

    Assert.Single(deleted);
    Assert.Single(added);
}

[Fact]
public void Match_FuzzyTitleMatch_RenamedChapter()
{
    var source = new List<ChapterNode>
    {
        new() { Key = "1", Title = "System Architecture Overview" }
    };
    var target = new List<ChapterNode>
    {
        new() { Key = "5", Title = "System Architecture Overview (Updated)" }
    };

    var pairs = ChapterMatcher.Match(source, target, titleSimilarityThreshold: 0.7);
    Assert.Single(pairs);
    Assert.NotNull(pairs[0].Source);
    Assert.NotNull(pairs[0].Target);
}
```
