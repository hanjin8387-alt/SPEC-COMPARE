using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class ChapterSegmenter
{
    private static readonly Regex NumberedHeadingPattern =
        new(@"^(?<key>\d{1,3}(?:\.\d{1,3}){0,5})\s+(?<title>\S.+)$", RegexOptions.Compiled);

    private static readonly Regex SectionOrChapterPattern =
        new(@"^(?<kind>SECTION|CHAPTER)\s+(?<key>\d{1,3}(?:\.\d{1,3}){0,5})\s*[:\-]?\s*(?<title>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TocHintPattern =
        new(@"^(?<key>\d{1,3}(?:\.\d{1,3}){0,5})\s+(?<title>.+?)\s*(?:\.{2,}\s*)?(?<page>\d{1,4})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DatePattern =
        new(@"\b(?:\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4})\b", RegexOptions.Compiled);

    private static readonly Regex MeasurementLeadingPattern =
        new(@"^\s*\d+(?:[.,]\d+)?\s*(?:mm|cm|m|kg|g|lb|hz|khz|mhz|ghz|v|ma|a|%)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ChapterNode> Segment(IReadOnlyList<PageText> pages, ChapterSegmentationOptions? options = null)
    {
        if (pages is null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        if (pages.Count == 0)
        {
            return new List<ChapterNode>();
        }

        options ??= new ChapterSegmentationOptions();

        var tocHints = BuildTocHints(pages, options);
        var roots = new List<ChapterNode>();
        var stack = new Stack<ChapterNode>();
        var duplicateCounter = new Dictionary<string, int>(StringComparer.Ordinal);
        ChapterNode? current = null;
        ChapterNode? preamble = null;
        var preambleAdded = false;

        foreach (var page in pages)
        {
            var pageContext = BuildPageContext(page, options);
            var lines = TextUtilities.SplitLines(page.RawText);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (TryParseHeading(line, out var heading) &&
                    IsAcceptedHeading(line, heading, pageContext, tocHints, options))
                {
                    if (current is not null && current.PageEnd < page.PageNumber)
                    {
                        current.PageEnd = page.PageNumber;
                    }

                    var uniqueKey = EnsureUniqueKey(heading.Key, duplicateCounter);
                    var node = new ChapterNode
                    {
                        Key = uniqueKey,
                        Title = heading.Title,
                        Level = CalculateLevel(heading.Key),
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

    private static bool TryParseHeading(string line, out ParsedHeading heading)
    {
        heading = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var sectionMatch = SectionOrChapterPattern.Match(line);
        if (sectionMatch.Success)
        {
            var key = NormalizeHeadingKey(sectionMatch.Groups["key"].Value);
            var title = sectionMatch.Groups["title"].Value.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"{sectionMatch.Groups["kind"].Value.ToUpperInvariant()} {key}";
            }

            heading = new ParsedHeading(key, title, HeadingKind.SectionOrChapter);
            return true;
        }

        var numberedMatch = NumberedHeadingPattern.Match(line);
        if (numberedMatch.Success)
        {
            var key = NormalizeHeadingKey(numberedMatch.Groups["key"].Value);
            var title = numberedMatch.Groups["title"].Value.Trim();
            heading = new ParsedHeading(key, title, HeadingKind.Numbered);
            return true;
        }

        return false;
    }

    private static bool IsAcceptedHeading(
        string line,
        ParsedHeading heading,
        PageHeadingContext context,
        HashSet<string> tocHints,
        ChapterSegmentationOptions options)
    {
        if (!IsReasonableHeading(heading, options))
        {
            return false;
        }

        if (ContainsDateLikeToken(line) || MeasurementLeadingPattern.IsMatch(heading.Title))
        {
            return false;
        }

        if (LooksLikeListItem(heading))
        {
            return false;
        }

        var score = heading.Kind == HeadingKind.SectionOrChapter ? 0.45d : 0.20d;

        if (heading.Key.Contains('.', StringComparison.Ordinal))
        {
            score += 0.08d;
        }

        if (tocHints.Contains(heading.Key))
        {
            score += 0.30d;
        }

        if (LooksLikeHeadingCase(heading.Title))
        {
            score += 0.10d;
        }

        if (!EndsWithSentencePunctuation(heading.Title))
        {
            score += 0.05d;
        }

        if (context.TryGetLineFont(line, out var lineFont) &&
            lineFont > 0 &&
            context.MedianFontSize > 0 &&
            lineFont >= context.MedianFontSize * options.LayoutHeadingFontRatio)
        {
            score += 0.22d;
        }

        return score >= options.MinHeadingScore;
    }

    private static bool IsReasonableHeading(ParsedHeading heading, ChapterSegmentationOptions options)
    {
        if (string.IsNullOrWhiteSpace(heading.Key) || string.IsNullOrWhiteSpace(heading.Title))
        {
            return false;
        }

        if (heading.Title.Length > options.MaxHeadingLength)
        {
            return false;
        }

        var words = heading.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.Length <= options.MaxHeadingWords;
    }

    private static bool ContainsDateLikeToken(string text)
    {
        return DatePattern.IsMatch(text);
    }

    private static bool LooksLikeListItem(ParsedHeading heading)
    {
        if (heading.Key.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (heading.Title.Length == 0)
        {
            return true;
        }

        var first = heading.Title[0];
        if (char.IsLower(first) && heading.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 4)
        {
            return true;
        }

        return heading.Title.EndsWith(".", StringComparison.Ordinal) &&
               heading.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 6;
    }

    private static bool LooksLikeHeadingCase(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        var uppercaseWords = words.Count(word => word.Length > 0 && word.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)));
        if (uppercaseWords == words.Length)
        {
            return true;
        }

        var capitalizedWords = words.Count(word =>
            word.Length > 0 &&
            char.IsLetter(word[0]) &&
            char.IsUpper(word[0]));

        return capitalizedWords >= Math.Max(1, (int)Math.Ceiling(words.Length * 0.5));
    }

    private static bool EndsWithSentencePunctuation(string title)
    {
        return title.EndsWith(".", StringComparison.Ordinal) ||
               title.EndsWith(";", StringComparison.Ordinal) ||
               title.EndsWith("?", StringComparison.Ordinal) ||
               title.EndsWith("!", StringComparison.Ordinal);
    }

    private static HashSet<string> BuildTocHints(IReadOnlyList<PageText> pages, ChapterSegmentationOptions options)
    {
        var hints = new HashSet<string>(StringComparer.Ordinal);
        var scanPages = Math.Min(options.TocScanPageCount, pages.Count);

        for (var i = 0; i < scanPages; i++)
        {
            var lines = TextUtilities.SplitLines(pages[i].RawText);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var tocMatch = TocHintPattern.Match(line);
                if (!tocMatch.Success)
                {
                    continue;
                }

                hints.Add(NormalizeHeadingKey(tocMatch.Groups["key"].Value));
            }
        }

        return hints;
    }

    private static PageHeadingContext BuildPageContext(PageText page, ChapterSegmentationOptions options)
    {
        if (page.Words.Count == 0)
        {
            return PageHeadingContext.Empty;
        }

        var orderedWords = page.Words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.Y)
            .ThenBy(word => word.X)
            .ToList();

        var lines = new List<List<WordInfo>>();
        var yAnchors = new List<double>();

        foreach (var word in orderedWords)
        {
            var index = -1;
            for (var i = 0; i < yAnchors.Count; i++)
            {
                if (Math.Abs(yAnchors[i] - word.Y) <= 2.0d)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                lines.Add(new List<WordInfo> { word });
                yAnchors.Add(word.Y);
                continue;
            }

            lines[index].Add(word);
        }

        var lineFonts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var bucket in lines)
        {
            var lineText = string.Join(
                " ",
                bucket.OrderBy(word => word.X).Select(word => word.Text));

            var normalizedLine = TextNormalizer.Normalize(lineText);
            if (normalizedLine.Length == 0)
            {
                continue;
            }

            var lineFont = bucket.Max(word => word.FontSize);
            if (lineFonts.TryGetValue(normalizedLine, out var existingFont))
            {
                lineFonts[normalizedLine] = Math.Max(existingFont, lineFont);
            }
            else
            {
                lineFonts[normalizedLine] = lineFont;
            }
        }

        var fontSizes = page.Words
            .Select(word => word.FontSize)
            .Where(size => size > 0)
            .OrderBy(size => size)
            .ToArray();

        var medianFont = fontSizes.Length == 0 ? 0d : fontSizes[fontSizes.Length / 2];
        return new PageHeadingContext(medianFont, lineFonts);
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

    private static string NormalizeHeadingKey(string key)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return key.Trim();
        }

        var normalized = segments
            .Select(segment =>
            {
                if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }

                return segment.Trim();
            });

        return string.Join('.', normalized);
    }

    private enum HeadingKind
    {
        Numbered,
        SectionOrChapter
    }

    private readonly record struct ParsedHeading(string Key, string Title, HeadingKind Kind);

    private sealed class PageHeadingContext
    {
        public static readonly PageHeadingContext Empty = new(0d, new Dictionary<string, double>(StringComparer.Ordinal));

        private readonly IReadOnlyDictionary<string, double> _lineFonts;

        public PageHeadingContext(double medianFontSize, IReadOnlyDictionary<string, double> lineFonts)
        {
            MedianFontSize = medianFontSize;
            _lineFonts = lineFonts;
        }

        public double MedianFontSize { get; }

        public bool TryGetLineFont(string rawLine, out double fontSize)
        {
            var normalizedLine = TextNormalizer.Normalize(rawLine);
            if (normalizedLine.Length == 0)
            {
                fontSize = 0d;
                return false;
            }

            return _lineFonts.TryGetValue(normalizedLine, out fontSize);
        }
    }
}
