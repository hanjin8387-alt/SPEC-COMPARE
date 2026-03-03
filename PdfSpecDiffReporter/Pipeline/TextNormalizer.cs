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

    public static List<PageText> RemoveHeadersFooters(
        IReadOnlyList<PageText> pages,
        double yThresholdPercent = 0.10,
        int minConsecutivePages = 3,
        double similarityThreshold = 0.90)
    {
        if (pages is null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        if (pages.Count == 0)
        {
            return new List<PageText>();
        }

        if (yThresholdPercent <= 0 || yThresholdPercent >= 0.5)
        {
            throw new ArgumentOutOfRangeException(nameof(yThresholdPercent));
        }

        var zones = pages.Select(page => BuildZones(page, yThresholdPercent)).ToList();
        var repeatedHeaders = FindRepeatingZones(zones.Select(z => z.HeaderText).ToList(), minConsecutivePages, similarityThreshold);
        var repeatedFooters = FindRepeatingZones(zones.Select(z => z.FooterText).ToList(), minConsecutivePages, similarityThreshold);

        var cleanedPages = new List<PageText>(pages.Count);

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var zone = zones[i];

            var lines = TextUtilities.SplitLines(page.RawText);

            if (IsInRepeatedSet(zone.HeaderText, repeatedHeaders, similarityThreshold))
            {
                RemoveLinesFromTop(lines, zone.HeaderLines);
            }

            if (IsInRepeatedSet(zone.FooterText, repeatedFooters, similarityThreshold))
            {
                RemoveLinesFromBottom(lines, zone.FooterLines);
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
        normalized = normalized.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var perLine = normalized
            .Split('\n')
            .Select(line => SpacesRegex.Replace(line.Trim(), " "));

        normalized = string.Join('\n', perLine);
        normalized = ExcessBlankLinesRegex.Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    private static ZoneSnapshot BuildZones(PageText page, double yThresholdPercent)
    {
        if (page.Words.Count == 0)
        {
            return new ZoneSnapshot(string.Empty, Array.Empty<string>(), string.Empty, Array.Empty<string>());
        }

        var minY = page.Words.Min(word => word.Y);
        var maxY = page.Words.Max(word => word.Y);
        var range = maxY - minY;

        if (range <= 0)
        {
            return new ZoneSnapshot(string.Empty, Array.Empty<string>(), string.Empty, Array.Empty<string>());
        }

        var headerThreshold = maxY - (range * yThresholdPercent);
        var footerThreshold = minY + (range * yThresholdPercent);

        var headerWords = page.Words.Where(word => word.Y >= headerThreshold).ToList();
        var footerWords = page.Words.Where(word => word.Y <= footerThreshold).ToList();

        var headerLines = ExtractLines(headerWords, topToBottom: true);
        var footerLines = ExtractLines(footerWords, topToBottom: true);

        var headerText = Normalize(string.Join('\n', headerLines));
        var footerText = Normalize(string.Join('\n', footerLines));

        return new ZoneSnapshot(headerText, headerLines, footerText, footerLines);
    }

    private static List<string> ExtractLines(List<WordInfo> words, bool topToBottom)
    {
        if (words.Count == 0)
        {
            return new List<string>();
        }

        const double sameLineTolerance = 2.0;
        var ordered = topToBottom
            ? words.OrderByDescending(word => word.Y).ThenBy(word => word.X).ToList()
            : words.OrderBy(word => word.Y).ThenBy(word => word.X).ToList();

        var lineBuckets = new List<List<WordInfo>>();
        var lineY = new List<double>();

        foreach (var word in ordered)
        {
            var bucketIndex = -1;
            for (var i = 0; i < lineY.Count; i++)
            {
                if (Math.Abs(lineY[i] - word.Y) <= sameLineTolerance)
                {
                    bucketIndex = i;
                    break;
                }
            }

            if (bucketIndex < 0)
            {
                lineBuckets.Add(new List<WordInfo> { word });
                lineY.Add(word.Y);
            }
            else
            {
                lineBuckets[bucketIndex].Add(word);
            }
        }

        var lines = new List<string>();
        foreach (var bucket in lineBuckets)
        {
            var line = string.Join(
                " ",
                bucket.OrderBy(word => word.X).Select(word => word.Text));

            var normalizedLine = Normalize(line);
            if (!string.IsNullOrWhiteSpace(normalizedLine))
            {
                lines.Add(normalizedLine);
            }
        }

        return lines;
    }

    private static HashSet<string> FindRepeatingZones(
        List<string> zoneTexts,
        int minConsecutivePages,
        double similarityThreshold)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (zoneTexts.Count < minConsecutivePages)
        {
            return result;
        }

        for (var i = 0; i <= zoneTexts.Count - minConsecutivePages; i++)
        {
            var current = zoneTexts[i];
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var runCount = 1;
            for (var j = i + 1; j < zoneTexts.Count; j++)
            {
                if (!IsSimilarNormalized(current, zoneTexts[j], similarityThreshold))
                {
                    break;
                }

                runCount++;
            }

            if (runCount >= minConsecutivePages)
            {
                for (var k = i; k < i + runCount; k++)
                {
                    result.Add(zoneTexts[k]);
                }
            }
        }

        return result;
    }

    private static bool IsInRepeatedSet(string zoneText, HashSet<string> repeatedZones, double similarityThreshold)
    {
        if (string.IsNullOrWhiteSpace(zoneText) || repeatedZones.Count == 0)
        {
            return false;
        }

        foreach (var repeated in repeatedZones)
        {
            if (IsSimilarNormalized(zoneText, repeated, similarityThreshold))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimilar(string left, string right, double threshold)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return IsSimilarNormalized(normalizedLeft, normalizedRight, threshold);
    }

    private static bool IsSimilarNormalized(string left, string right, double threshold)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (left.Equals(right, StringComparison.Ordinal))
        {
            return true;
        }

        var similarity = SimilarityCalculator.Calculate(left, right);
        return similarity >= threshold;
    }

    private static void RemoveLinesFromTop(List<string> lines, IReadOnlyList<string> candidates)
    {
        if (lines.Count == 0 || candidates.Count == 0)
        {
            return;
        }

        var normalizedCandidates = candidates
            .Select(Normalize)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToList();

        foreach (var candidate in normalizedCandidates)
        {
            var topSearchLimit = Math.Min(lines.Count, 8);
            for (var i = 0; i < topSearchLimit; i++)
            {
                if (Normalize(lines[i]).Equals(candidate, StringComparison.Ordinal))
                {
                    lines.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static void RemoveLinesFromBottom(List<string> lines, IReadOnlyList<string> candidates)
    {
        if (lines.Count == 0 || candidates.Count == 0)
        {
            return;
        }

        var normalizedCandidates = candidates
            .Select(Normalize)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToList();

        for (var c = normalizedCandidates.Count - 1; c >= 0; c--)
        {
            var candidate = normalizedCandidates[c];
            var bottomStart = Math.Max(0, lines.Count - 8);
            for (var i = lines.Count - 1; i >= bottomStart; i--)
            {
                if (Normalize(lines[i]).Equals(candidate, StringComparison.Ordinal))
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

    private sealed record ZoneSnapshot(
        string HeaderText,
        IReadOnlyList<string> HeaderLines,
        string FooterText,
        IReadOnlyList<string> FooterLines);
}
