using System;
using System.Collections.Generic;
using System.Linq;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

internal static class WordLineBuilder
{
    public static IReadOnlyList<TextLine> BuildLines(
        int pageNumber,
        IReadOnlyList<WordInfo> words,
        double tolerance,
        CancellationToken cancellationToken = default)
    {
        if (words is null)
        {
            throw new ArgumentNullException(nameof(words));
        }

        if (words.Count == 0)
        {
            return Array.Empty<TextLine>();
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
            cancellationToken.ThrowIfCancellationRequested();

            var bucketIndex = -1;
            for (var index = 0; index < lineAnchors.Count; index++)
            {
                if (Math.Abs(lineAnchors[index] - word.Y) <= tolerance)
                {
                    bucketIndex = index;
                    break;
                }
            }

            if (bucketIndex < 0)
            {
                lineBuckets.Add(new List<WordInfo> { word });
                lineAnchors.Add(word.Y);
                continue;
            }

            lineBuckets[bucketIndex].Add(word);
        }

        var lines = new List<TextLine>(lineBuckets.Count);
        foreach (var bucket in lineBuckets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var orderedBucket = bucket.OrderBy(word => word.X).ToList();
            var rawText = string.Join(" ", orderedBucket.Select(word => word.Text));
            var normalized = TextNormalizer.Normalize(rawText);
            if (normalized.Length == 0)
            {
                continue;
            }

            lines.Add(new TextLine(
                pageNumber,
                rawText.Trim(),
                normalized,
                orderedBucket.Average(word => word.Y),
                orderedBucket.Min(word => word.X),
                orderedBucket.Max(word => word.X),
                orderedBucket.Max(word => word.FontSize),
                orderedBucket.Average(word => word.FontSize),
                orderedBucket.Count));
        }

        return lines
            .OrderByDescending(line => line.Y)
            .ThenBy(line => line.MinX)
            .ToArray();
    }
}
