# Architecture

## Why the structure changed

The original repository concentrated CLI parsing, validation, orchestration, cancellation wiring, exception handling, and report generation inside `Program.cs`. It also hardcoded Windows publishing defaults into the application project and truncated diff text in the domain model.

This refactor moved the repository toward a production-grade CLI shape:

- `Program.cs` is now a composition root only.
- orchestration lives in `Application/SpecCompareApplication.cs`
- pipeline stages accept `CancellationToken`
- diff text is preserved in core models
- chapter matching emits evidence instead of an opaque title score
- report generation surfaces previews, full text, match evidence, and diagnostics
- the app project is no longer tied to `win-x64`

## Current runtime flow

1. `Program.cs` parses CLI arguments into `SpecCompareRequest`.
2. `SpecCompareApplication` validates input paths, loads config, resolves effective options, and owns the progress UI.
3. `PdfInputLoader` opens the PDFs as read-only streams.
4. `TextExtractor` reads page text and positioned words with PdfPig.
5. `TextNormalizer` removes repeated headers/footers and normalizes page text.
6. `ChapterSegmenter` identifies chapter nodes and freezes them into read-only models.
7. `ChapterMatcher` performs:
   - exact-key anchors
   - near-exact title anchors
   - weighted assignment for remaining chapters
8. `DiffEngine` builds stable text blocks, aligns them, and produces `DiffItem` results without truncating content.
9. `ExcelReporter` writes the workbook and adds diagnostics and explainability sheets.

## Key design points

- Shared word-to-line reconstruction:
  `Pipeline/WordLineBuilder.cs` is used by normalization and chapter segmentation so layout heuristics stay consistent.

- Explainable matching:
  `ChapterMatchEvidence` records match kind, individual score components, and reasons so pairing decisions can be inspected in the workbook.

- Stable error identity:
  `ClassifiedException` carries correlation ID and exit classification through wrapping and sanitization.

- Report fidelity:
  previews are report-only; full before/after text remains in domain results and is written to the `FullText` worksheet.

## Test strategy

The test project now covers:

- config precedence and validation
- exception correlation behavior
- chapter segmentation heuristics
- chapter matching anchors and context handling
- diff regressions and full-text preservation
- report workbook structure
- cancellation behavior
- a synthetic PDF-to-workbook integration path
