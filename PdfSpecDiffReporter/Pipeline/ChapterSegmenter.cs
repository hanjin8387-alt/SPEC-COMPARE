using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class ChapterSegmenter
{
    private static readonly Regex NumberedHeadingPattern =
        new(@"^(\d+(?:\.\d+)*)\s+(.+)$", RegexOptions.Compiled);

    private static readonly Regex SectionOrChapterPattern =
        new(@"^(SECTION|CHAPTER)\s+(\d+(?:\.\d+)*)\s*[:\-]?\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ChapterNode> Segment(IReadOnlyList<PageText> pages)
    {
        if (pages is null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        if (pages.Count == 0)
        {
            return new List<ChapterNode>();
        }

        var roots = new List<ChapterNode>();
        var stack = new Stack<ChapterNode>();
        var duplicateCounter = new Dictionary<string, int>(StringComparer.Ordinal);
        ChapterNode? current = null;
        ChapterNode? preamble = null;
        var preambleAdded = false;

        foreach (var page in pages)
        {
            var lines = TextUtilities.SplitLines(page.RawText);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (TryParseHeading(line, out var headingKey, out var headingTitle))
                {
                    if (current is not null && current.PageEnd < page.PageNumber)
                    {
                        current.PageEnd = page.PageNumber;
                    }

                    var uniqueKey = EnsureUniqueKey(headingKey, duplicateCounter);
                    var level = CalculateLevel(headingKey);
                    var node = new ChapterNode
                    {
                        Key = uniqueKey,
                        Title = headingTitle,
                        Level = level,
                        PageStart = page.PageNumber,
                        PageEnd = page.PageNumber
                    };

                    while (stack.Count > 0 && stack.Peek().Level >= node.Level)
                    {
                        stack.Pop();
                    }

                    if (stack.Count == 0)
                    {
                        roots.Add(node);
                    }
                    else
                    {
                        stack.Peek().Children.Add(node);
                    }

                    stack.Push(node);
                    current = node;
                    continue;
                }

                if (current is null)
                {
                    preamble ??= new ChapterNode
                    {
                        Key = "0",
                        Title = "(Preamble)",
                        Level = 0,
                        PageStart = page.PageNumber,
                        PageEnd = page.PageNumber
                    };

                    if (!preambleAdded)
                    {
                        roots.Insert(0, preamble);
                        preambleAdded = true;
                    }

                    current = preamble;
                }

                current.Content.AppendLine(rawLine);
                current.PageEnd = page.PageNumber;
            }
        }

        if (roots.Count == 0)
        {
            var root = new ChapterNode
            {
                Key = "0",
                Title = "(Document)",
                Level = 0,
                PageStart = pages.First().PageNumber,
                PageEnd = pages.Last().PageNumber
            };

            foreach (var page in pages)
            {
                root.Content.AppendLine(page.RawText);
            }

            roots.Add(root);
        }

        return roots;
    }

    private static bool TryParseHeading(string line, out string key, out string title)
    {
        key = string.Empty;
        title = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var numberedMatch = NumberedHeadingPattern.Match(line);
        if (numberedMatch.Success)
        {
            key = numberedMatch.Groups[1].Value;
            title = numberedMatch.Groups[2].Value.Trim();
            return true;
        }

        var sectionMatch = SectionOrChapterPattern.Match(line);
        if (sectionMatch.Success)
        {
            key = sectionMatch.Groups[2].Value;
            title = sectionMatch.Groups[3].Value.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"{sectionMatch.Groups[1].Value.ToUpperInvariant()} {sectionMatch.Groups[2].Value}";
            }

            return true;
        }

        return false;
    }

    private static int CalculateLevel(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? 0 : key.Split('.').Length;
    }

    private static string EnsureUniqueKey(string key, Dictionary<string, int> duplicateCounter)
    {
        if (!duplicateCounter.TryGetValue(key, out var count))
        {
            duplicateCounter[key] = 0;
            return key;
        }

        count++;
        duplicateCounter[key] = count;
        return $"{key}_dup{count}";
    }
}
