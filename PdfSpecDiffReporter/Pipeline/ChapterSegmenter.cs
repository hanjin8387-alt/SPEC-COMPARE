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

    public static List<ChapterNode> Segment(
        IReadOnlyList<PageText> pages,
        ChapterSegmentationOptions? options = null,
        CancellationToken cancellationToken = default)
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

        var tocHints = BuildTocHints(pages, options, cancellationToken);
        var roots = new List<ChapterNodeBuilder>();
        var stack = new Stack<ChapterNodeBuilder>();
        var duplicateCounter = new Dictionary<string, int>(StringComparer.Ordinal);
        ChapterNodeBuilder? current = null;
        ChapterNodeBuilder? preamble = null;
        var preambleAdded = false;
        var order = 0;

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageContext = BuildPageContext(page, cancellationToken);
            var lines = GetContentLines(page);
            foreach (var contentLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawLine = contentLine.Text;
                var line = rawLine.Trim();

                if (TryParseHeading(line, out var heading) &&
                    IsAcceptedHeading(line, heading, pageContext, tocHints, options))
                {
                    if (current is not null && current.PageEnd < page.PageNumber)
                    {
                        current.PageEnd = page.PageNumber;
                    }

                    while (stack.Count > 0 && stack.Peek().Level >= CalculateLevel(heading.Key))
                    {
                        stack.Pop();
                    }

                    var parent = stack.Count == 0 ? null : stack.Peek();
                    var uniqueKey = EnsureUniqueKey(heading.Key, duplicateCounter);
                    var node = new ChapterNodeBuilder(
                        uniqueKey,
                        heading.Key,
                        heading.Title,
                        CalculateLevel(heading.Key),
                        page.PageNumber,
                        order++,
                        parent);

                    if (parent is null)
                    {
                        roots.Add(node);
                    }
                    else
                    {
                        parent.Children.Add(node);
                    }

                    stack.Push(node);
                    current = node;
                    continue;
                }

                if (current is null)
                {
                    preamble ??= new ChapterNodeBuilder("0", "0", "(Preamble)", 0, page.PageNumber, order++, parent: null);

                    if (!preambleAdded)
                    {
                        roots.Insert(0, preamble);
                        preambleAdded = true;
                    }

                    current = preamble;
                }

                current.Lines.Add(contentLine);
                current.PageEnd = page.PageNumber;
            }
        }

        if (roots.Count == 0)
        {
            var root = new ChapterNodeBuilder(
                "0",
                "0",
                "(Document)",
                0,
                pages.First().PageNumber,
                order++,
                parent: null)
            {
                PageEnd = pages.Last().PageNumber
            };

            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                root.Lines.AddRange(GetContentLines(page));
            }

            roots.Add(root);
        }

        return roots
            .Select(Freeze)
            .ToList();
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

        if (TocHintPattern.IsMatch(line))
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

    private static HashSet<string> BuildTocHints(
        IReadOnlyList<PageText> pages,
        ChapterSegmentationOptions options,
        CancellationToken cancellationToken)
    {
        var hints = new HashSet<string>(StringComparer.Ordinal);
        var scanPages = Math.Min(options.TocScanPageCount, pages.Count);

        for (var pageIndex = 0; pageIndex < scanPages; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lines = TextUtilities.SplitLines(pages[pageIndex].RawText);
            foreach (var rawLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

    private static PageHeadingContext BuildPageContext(PageText page, CancellationToken cancellationToken)
    {
        var lines = page.Lines;
        if (lines.Count == 0)
        {
            return PageHeadingContext.Empty;
        }

        var lineFonts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (line.NormalizedText.Length == 0)
            {
                continue;
            }

            lineFonts[line.NormalizedText] = Math.Max(
                lineFonts.TryGetValue(line.NormalizedText, out var existingFont) ? existingFont : 0d,
                line.MaxFontSize);
        }

        var fontSizes = lines
            .Where(line => line.AverageFontSize > 0d)
            .SelectMany(line => Enumerable.Repeat(line.AverageFontSize, Math.Max(1, line.WordCount)))
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

    private static IReadOnlyList<TextLine> GetContentLines(PageText page)
    {
        if (page.Lines.Count > 0)
        {
            return page.Lines;
        }

        return TextUtilities.SplitLines(page.RawText)
            .Select((line, index) => new TextLine(
                page.PageNumber,
                line,
                TextNormalizer.Normalize(line),
                -index,
                0d,
                0d,
                0d,
                0d,
                0))
            .ToArray();
    }

    private static ChapterNode Freeze(ChapterNodeBuilder builder)
    {
        var blocks = TextBlockBuilder.BuildBlocks(builder.Lines);

        return new ChapterNode
        {
            Key = builder.Key,
            MatchKey = builder.MatchKey,
            Title = builder.Title,
            Level = builder.Level,
            Blocks = blocks,
            Children = builder.Children.Select(Freeze).ToArray(),
            PageStart = builder.PageStart,
            PageEnd = builder.PageEnd,
            Order = builder.Order,
            ParentKey = builder.ParentKey,
            ParentMatchKey = builder.ParentMatchKey,
            ParentTitle = builder.ParentTitle
        };
    }

    private enum HeadingKind
    {
        Numbered,
        SectionOrChapter
    }

    private readonly record struct ParsedHeading(string Key, string Title, HeadingKind Kind);

    private sealed class ChapterNodeBuilder
    {
        public ChapterNodeBuilder(
            string key,
            string matchKey,
            string title,
            int level,
            int pageStart,
            int order,
            ChapterNodeBuilder? parent)
        {
            Key = key;
            MatchKey = matchKey;
            Title = title;
            Level = level;
            PageStart = pageStart;
            PageEnd = pageStart;
            Order = order;
            ParentKey = parent?.Key ?? string.Empty;
            ParentMatchKey = parent?.MatchKey ?? string.Empty;
            ParentTitle = parent?.Title ?? string.Empty;
        }

        public string Key { get; }

        public string MatchKey { get; }

        public string Title { get; }

        public int Level { get; }

        public List<TextLine> Lines { get; } = new();

        public List<ChapterNodeBuilder> Children { get; } = new();

        public int PageStart { get; }

        public int PageEnd { get; set; }

        public int Order { get; }

        public string ParentKey { get; }

        public string ParentMatchKey { get; }

        public string ParentTitle { get; }
    }

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
