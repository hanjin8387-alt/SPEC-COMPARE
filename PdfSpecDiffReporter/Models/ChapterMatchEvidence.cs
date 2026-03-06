using System;
using System.Collections.Generic;

namespace PdfSpecDiffReporter.Models;

public enum ChapterMatchKind
{
    ExactKeyAnchor,
    NearExactAnchor,
    WeightedAssignment
}

public sealed record ChapterMatchEvidence(
    ChapterMatchKind Kind,
    double OverallScore,
    double KeyScore,
    double TitleScore,
    double LevelScore,
    double OrderScore,
    double ContextScore,
    IReadOnlyList<string> Reasons)
{
    public static readonly ChapterMatchEvidence None = new(
        ChapterMatchKind.WeightedAssignment,
        0d,
        0d,
        0d,
        0d,
        0d,
        0d,
        Array.Empty<string>());
}
