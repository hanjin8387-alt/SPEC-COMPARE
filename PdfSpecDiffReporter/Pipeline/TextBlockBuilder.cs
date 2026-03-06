using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Pipeline;

public static class TextBlockBuilder
{
    private static readonly Regex NumberedLinePattern =
        new(@"^(?:\d+(?:\.\d+)*\s+\S+|\d+[.)]\s+|\(?[A-Za-z]\)\s+)", RegexOptions.Compiled);

    private static readonly Regex BulletLinePattern =
        new(@"^(?:[-*•]\s+)", RegexOptions.Compiled);

    public static IReadOnlyList<TextBlock> BuildBlocks(
        IReadOnlyList<TextLine> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        if (lines.Count == 0)
        {
            return Array.Empty<TextBlock>();
        }

        var blocks = new List<TextBlock>();
        var currentOriginal = new List<string>();
        var currentNormalized = new List<string>();
        string? previousNormalized = null;

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var original = line.Text.Trim();
            var normalized = line.NormalizedText;
            if (normalized.Length == 0)
            {
                FlushCurrentBlock(blocks, currentOriginal, currentNormalized);
                previousNormalized = null;
                continue;
            }

            if (currentOriginal.Count > 0 &&
                previousNormalized is not null &&
                ShouldStartNewBlock(previousNormalized, normalized))
            {
                FlushCurrentBlock(blocks, currentOriginal, currentNormalized);
            }

            currentOriginal.Add(original);
            currentNormalized.Add(normalized);
            previousNormalized = normalized;
        }

        FlushCurrentBlock(blocks, currentOriginal, currentNormalized);
        return blocks;
    }

    public static string CombineOriginalText(IReadOnlyList<TextBlock> blocks)
    {
        return TextBlock.CombineOriginalText(blocks);
    }

    private static bool ShouldStartNewBlock(string previousLine, string currentLine)
    {
        if (LooksLikeStructuralLine(previousLine) || LooksLikeStructuralLine(currentLine))
        {
            return true;
        }

        return EndsWithTerminalPunctuation(previousLine) && StartsLikeSentence(currentLine);
    }

    private static bool LooksLikeStructuralLine(string line)
    {
        if (NumberedLinePattern.IsMatch(line) || BulletLinePattern.IsMatch(line))
        {
            return true;
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens.Length > 8)
        {
            return false;
        }

        var uppercaseTokens = tokens.Count(token => token.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)));
        return uppercaseTokens >= Math.Max(1, (int)Math.Ceiling(tokens.Length * 0.7d));
    }

    private static bool EndsWithTerminalPunctuation(string line)
    {
        return line.EndsWith(".", StringComparison.Ordinal) ||
               line.EndsWith("!", StringComparison.Ordinal) ||
               line.EndsWith("?", StringComparison.Ordinal) ||
               line.EndsWith(":", StringComparison.Ordinal) ||
               line.EndsWith(";", StringComparison.Ordinal);
    }

    private static bool StartsLikeSentence(string line)
    {
        var firstLetter = line.FirstOrDefault(char.IsLetterOrDigit);
        return firstLetter != default && (char.IsUpper(firstLetter) || char.IsDigit(firstLetter));
    }

    private static void FlushCurrentBlock(
        ICollection<TextBlock> blocks,
        ICollection<string> currentOriginal,
        ICollection<string> currentNormalized)
    {
        if (currentOriginal.Count == 0 || currentNormalized.Count == 0)
        {
            return;
        }

        blocks.Add(new TextBlock(
            string.Join('\n', currentOriginal).Trim(),
            string.Join('\n', currentNormalized).Trim()));

        currentOriginal.Clear();
        currentNormalized.Clear();
    }
}
