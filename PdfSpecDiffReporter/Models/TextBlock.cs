using System.Text;

namespace PdfSpecDiffReporter.Models;

public sealed record TextBlock(
    string OriginalText,
    string NormalizedText)
{
    public static string CombineOriginalText(IReadOnlyList<TextBlock> blocks)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(blocks[index].OriginalText);
        }

        return builder.ToString().Trim();
    }
}
