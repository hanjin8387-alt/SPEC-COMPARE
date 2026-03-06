# SPEC-COMPARE

SPEC-COMPARE is a .NET 8 CLI that compares two PDF specification documents, matches chapters, computes textual differences, and writes an Excel workbook with summary, change details, full text, match evidence, unmatched chapters, and diagnostics.

## CLI usage

```bash
dotnet run --project PdfSpecDiffReporter -- source.pdf target.pdf
dotnet run --project PdfSpecDiffReporter -- source.pdf target.pdf --output reports/spec-diff.xlsx
dotnet run --project PdfSpecDiffReporter -- source.pdf target.pdf --config spec-compare.json
dotnet run --project PdfSpecDiffReporter -- source.pdf target.pdf --diff-threshold 0.82 --chapter-match-threshold 0.68
```

Arguments:

- `source_pdf`: path to the baseline PDF
- `target_pdf`: path to the revised PDF

Options:

- `--output`, `-o`: output workbook path, default `diff_report.xlsx`
- `--config`, `-c`: optional JSON config path
- `--diff-threshold`: override similarity threshold for diff classification
- `--chapter-match-threshold`: override similarity threshold for chapter matching

## Config example

```json
{
  "diffThreshold": 0.85,
  "chapterMatchThreshold": 0.7,
  "textNormalization": {
    "headerFooterBandPercent": 0.1,
    "minRepeatingPages": 3,
    "repeatingSimilarityThreshold": 0.9,
    "lineMergeTolerance": 2.0,
    "zoneLineLimit": 3,
    "searchWindow": 8,
    "minZoneTextLength": 4
  },
  "chapterSegmentation": {
    "tocScanPageCount": 5,
    "layoutHeadingFontRatio": 1.08,
    "minHeadingScore": 0.65,
    "maxHeadingWords": 16,
    "maxHeadingLength": 120
  }
}
```

Config precedence is `CLI override -> config file -> built-in default`.

## Supported platforms

- Windows
- Linux

The application project is framework-neutral. RID-specific single-file publishing is now an explicit publish choice, not a project default.

## Architecture summary

- `PdfSpecDiffReporter/Program.cs` is the composition root only.
- `PdfSpecDiffReporter/Application/SpecCompareApplication.cs` owns request validation, orchestration, console UX, and exit handling.
- `PdfSpecDiffReporter/Pipeline` contains PDF input loading, text extraction, normalization, chapter segmentation, matching, and diffing.
- `PdfSpecDiffReporter/Helpers/ExcelReporter.cs` builds the workbook output and includes previews, full text, match evidence, and diagnostics.
- `PdfSpecDiffReporter/Models` contains immutable or read-only pipeline/result models.

More detail is in [`docs/architecture.md`](docs/architecture.md).

## Development

Restore with locked dependencies:

```bash
dotnet restore PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj --locked-mode
```

Build:

```bash
dotnet build PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj -c Release --no-restore --warnaserror
```

Test:

```bash
dotnet test PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage"
```

Manual publish example:

```bash
dotnet publish PdfSpecDiffReporter/PdfSpecDiffReporter.csproj -c Release -r linux-x64 --self-contained false /p:PublishSingleFile=true
```

## Known limitations

- PDF text extraction quality still depends on the source document layout and font metadata.
- Matching is explainable and more stable than the previous greedy implementation, but heavily renumbered documents can still require threshold tuning.
- The Excel workbook preserves full diff text, which improves fidelity but can increase workbook size for very large documents.
