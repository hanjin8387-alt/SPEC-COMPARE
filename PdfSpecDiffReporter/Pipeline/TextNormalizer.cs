using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class TextNormalizer
{
    private static readonly Regex ControlCharsRegex =
        new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);

    private static readonly Regex SpacesRegex =
        new(@"[ \t]+", RegexOptions.Compiled);

    private static readonly Regex ExcessBlankLinesRegex =
        new(@"\n{3,}", RegexOptions.Compiled);

    private static readonly Regex PageNumberRegex =
        new(
            @"^\s*(?:-+\s*)?(?:page\s*)?\d+(?:\s*(?:of|/)\s*\d+)?(?:\s*-+)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VariableNumberRegex =
        new(@"\b\d+(?:[.,]\d+)?\b", RegexOptions.Compiled);

    public static List<PageText> RemoveHeadersFooters(
        IReadOnlyList<PageText> pages,
        TextNormalizationOptions? options = null)
    {
        if (pages is null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        if (pages.Count == 0)
        {
            return new List<PageText>();
        }

        options ??= new TextNormalizationOptions();

        var zones = pages.Select(page => BuildZones(page, options)).ToList();
        var repeatedHeaders = FindRepeatingZoneSignatures(
            zones.Select(zone => zone.HeaderSignature).ToList(),
            options.MinRepeatingPages,
            options.RepeatingSimilarityThreshold);

        var repeatedFooters = FindRepeatingZoneSignatures(
            zones.Select(zone => zone.FooterSignature).ToList(),
            options.MinRepeatingPages,
            options.RepeatingSimilarityThreshold);

        var cleanedPages = new List<PageText>(pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var zone = zones[i];
            var lines = TextUtilities.SplitLines(page.RawText);

            if (IsInRepeatedSet(zone.HeaderSignature, repeatedHeaders, options.RepeatingSimilarityThreshold))
            {
                RemoveLinesFromTop(lines, zone.HeaderLines, options.SearchWindow, options.RepeatingSimilarityThreshold);
            }

            if (IsInRepeatedSet(zone.FooterSignature, repeatedFooters, options.RepeatingSimilarityThreshold))
            {
                RemoveLinesFromBottom(lines, zone.FooterLines, options.SearchWindow, options.RepeatingSimilarityThreshold);
            }

            RemovePageNumberLine(lines);

            var rebuiltText = string.Join('\n', lines);
            var normalizedText = Normalize(rebuiltText);
            cleanedPages.Add(new PageText(page.PageNumber, normalizedText, page.Words));
        }

        return cleanedPages;
    }

    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var normalized = input.Normalize(NormalizationForm.FormC);
        normalized = ControlCharsRegex.Replace(normalized, string.Empty);
        normalized = normalized.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        var perLine = normalized
            .Split('\n')
            .Select(line => SpacesRegex.Replace(line.Trim(), " "));

        normalized = string.Join('\n', perLine);
        normalized = ExcessBlankLinesRegex.Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    private static ZoneSnapshot BuildZones(PageText page, TextNormalizationOptions options)
    {
        if (page.Words.Count == 0)
        {
            return ZoneSnapshot.Empty;
        }

        var lines = ExtractLines(page.Words, options.LineMergeTolerance);
        if (lines.Count < 2)
        {
            return ZoneSnapshot.Empty;
        }

        var minY = lines.Min(line => line.Y);
        var maxY = lines.Max(line => line.Y);
        var range = maxY - minY;
        if (range <= 0d)
        {
            return ZoneSnapshot.Empty;
        }

        var headerThreshold = maxY - (range * options.HeaderFooterBandPercent);
        var footerThreshold = minY + (range * options.HeaderFooterBandPercent);

        var headerLines = lines
            .Where(line => line.Y >= headerThreshold)
            .Select(line => line.Text)
            .Where(text => text.Length >= options.MinZoneTextLength)
            .Take(options.ZoneLineLimit)
            .ToList();

        var footerCandidates = lines
            .Where(line => line.Y <= footerThreshold)
            .Select(line => line.Text)
            .Where(text => text.Length >= options.MinZoneTextLength)
            .ToList();

        var footerLines = footerCandidates
            .Skip(Math.Max(0, footerCandidates.Count - options.ZoneLineLimit))
            .ToList();

        var headerSignature = CanonicalizeZoneText(string.Join('\n', headerLines));
        var footerSignature = CanonicalizeZoneText(string.Join('\n', footerLines));

        return new ZoneSnapshot(headerSignature, headerLines, footerSignature, footerLines);
    }

    private static List<LineInfo> ExtractLines(IReadOnlyList<WordInfo> words, double tolerance)
    {
        if (words.Count == 0)
        {
            return new List<LineInfo>();
        }

        var ordered = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.Y)
            .ThenBy(word => word.X)
            .ToList();

        var lineBuckets = new List<List<WordInfo>>();
        var lineAnchors = new List<double>();

        foreach (var word in ordered)
        {
            var bucketIndex = -1;
            for (var i = 0; i < lineAnchors.Count; i++)
            {
                if (Math.Abs(lineAnchors[i] - word.Y) <= tolerance)
                {
                    bucketIndex = i;
                    break;
                }
            }

            if (bucketIndex < 0)
            {
                lineBuckets.Add(new List<WordInfo> { word });
                lineAnchors.Add(word.Y);
            }
            else
            {
                lineBuckets[bucketIndex].Add(word);
            }
        }

        var lines = new List<LineInfo>(lineBuckets.Count);
        foreach (var bucket in lineBuckets)
        {
            var text = string.Join(" ", bucket.OrderBy(word => word.X).Select(word => word.Text));
            var normalized = Normalize(text);
            if (normalized.Length == 0)
            {
                continue;
            }

            var y = bucket.Average(word => word.Y);
            lines.Add(new LineInfo(normalized, y));
        }

        return lines
            .OrderByDescending(line => line.Y)
            .ToList();
    }

    private static string CanonicalizeZoneText(string input)
    {
        var normalized = Normalize(input).ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        normalized = VariableNumberRegex.Replace(normalized, "#");
        normalized = SpacesRegex.Replace(normalized, " ");
        return normalized.Trim();
    }

    private static HashSet<string> FindRepeatingZoneSignatures(
        IReadOnlyList<string> signatures,
        int minimumOccurrences,
        double similarityThreshold)
    {
        var repeated = new HashSet<string>(StringComparer.Ordinal);
        if (signatures.Count < minimumOccurrences)
        {
            return repeated;
        }

        for (var i = 0; i < signatures.Count; i++)
        {
            var baseSignature = signatures[i];
            if (string.IsNullOrWhiteSpace(baseSignature))
            {
                continue;
            }

            var similarCount = 0;
            for (var j = 0; j < signatures.Count; j++)
            {
                var candidate = signatures[j];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (IsSimilarNormalized(baseSignature, candidate, similarityThreshold))
                {
                    similarCount++;
                }
            }

            if (similarCount >= minimumOccurrences)
            {
                repeated.Add(baseSignature);
            }
        }

        return repeated;
    }

    private static bool IsInRepeatedSet(string signature, HashSet<string> repeated, double similarityThreshold)
    {
        if (signature.Length == 0 || repeated.Count == 0)
        {
            return false;
        }

        foreach (var candidate in repeated)
        {
            if (IsSimilarNormalized(signature, candidate, similarityThreshold))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimilarNormalized(string left, string right, double threshold)
    {
        if (left.Equals(right, StringComparison.Ordinal))
        {
            return true;
        }

        var similarity = SimilarityCalculator.Calculate(left, right, ignoreCase: true);
        return similarity >= threshold;
    }

    private static void RemoveLinesFromTop(
        List<string> lines,
        IReadOnlyList<string> candidates,
        int searchWindow,
        double similarityThreshold)
    {
        if (lines.Count == 0 || candidates.Count == 0)
        {
            return;
        }

        var normalizedCandidates = candidates
            .Select(Normalize)
            .Where(candidate => candidate.Length > 0)
            .ToList();

        foreach (var candidate in normalizedCandidates)
        {
            var topLimit = Math.Min(lines.Count, searchWindow);
            for (var i = 0; i < topLimit; i++)
            {
                var normalizedLine = Normalize(lines[i]);
                if (normalizedLine.Length == 0)
                {
                    continue;
                }

                if (IsSimilarNormalized(normalizedLine, candidate, similarityThreshold))
                {
                    lines.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static void RemoveLinesFromBottom(
        List<string> lines,
        IReadOnlyList<string> candidates,
        int searchWindow,
        double similarityThreshold)
    {
        if (lines.Count == 0 || candidates.Count == 0)
        {
            return;
        }

        var normalizedCandidates = candidates
            .Select(Normalize)
            .Where(candidate => candidate.Length > 0)
            .ToList();

        for (var c = normalizedCandidates.Count - 1; c >= 0; c--)
        {
            var candidate = normalizedCandidates[c];
            var bottomStart = Math.Max(0, lines.Count - searchWindow);
            for (var i = lines.Count - 1; i >= bottomStart; i--)
            {
                var normalizedLine = Normalize(lines[i]);
                if (normalizedLine.Length == 0)
                {
                    continue;
                }

                if (IsSimilarNormalized(normalizedLine, candidate, similarityThreshold))
                {
                    lines.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static void RemovePageNumberLine(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        if (PageNumberRegex.IsMatch(Normalize(lines[0])))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count == 0)
        {
            return;
        }

        if (PageNumberRegex.IsMatch(Normalize(lines[^1])))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private readonly record struct LineInfo(string Text, double Y);

    private sealed record ZoneSnapshot(
        string HeaderSignature,
        IReadOnlyList<string> HeaderLines,
        string FooterSignature,
        IReadOnlyList<string> FooterLines)
    {
        public static readonly ZoneSnapshot Empty =
            new(string.Empty, Array.Empty<string>(), string.Empty, Array.Empty<string>());
    }
}
