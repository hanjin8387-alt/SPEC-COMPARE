# Architecture

## Why the structure changed

The original repository concentrated CLI parsing, validation, orchestration, cancellation wiring, exception handling, and report generation inside `Program.cs`. It also hardcoded Windows publishing defaults into the application project and truncated diff text in the domain model.

This refactor moved the repository toward a production-grade CLI shape:

- `Program.cs` is now a composition root only.
- `SpecCompareApplication` is a thin facade over request resolution, pipeline orchestration, progress execution, diagnostics assembly, and outcome presentation services
- pipeline stages accept `CancellationToken`
- diff text is preserved in core models and preview truncation stays report-only
- chapter matching emits evidence instead of an opaque title score
- report generation is decomposed into a facade plus focused worksheet writers
- the app project is no longer tied to `win-x64`

## Current runtime flow

1. `Program.cs` parses CLI arguments into `SpecCompareRequest`.
2. `SpecCompareRequestResolver` validates input paths, loads config, and resolves effective thresholds and report settings.
3. `ConsoleProgressRunner` hosts the progress UI while `SpecComparePipeline` coordinates the run.
4. `DocumentPipeline` processes source and target documents concurrently through open, extract, normalize, and segment stages.
5. `TextExtractor` reads positioned words with PdfPig and converts them into shared `TextLine` data immediately.
6. `TextNormalizer` removes repeated headers/footers on shared line data without keeping `WordInfo` lists beyond extraction.
7. `ChapterSegmenter` identifies chapter nodes, freezes layout-aware `TextBlock` data, and retains full text only at chapter granularity.
8. `ChapterMatcher` performs:
   - exact-key anchors
   - near-exact title anchors
   - weighted assignment for remaining chapters
9. `DiffEngine` aligns shared `TextBlock` instances directly instead of re-splitting plain chapter strings.
10. `ExcelReporter` delegates workbook output to focused sheet writers and applies report policy such as preview length and `FullText` inclusion.
11. `SpecCompareOutcomePresenter` maps success, cancellation, and failures to console output and exit codes.

## Key design points

- Shared structured text:
  `Pipeline/TextExtractor.cs` converts positioned words into `TextLine` data once, `Pipeline/TextBlockBuilder.cs` groups those lines into layout-aware `TextBlock` instances, and the same structured path is reused through normalization, segmentation, and diffing.

- Explainable matching:
  `ChapterMatchEvidence` records match kind, individual score components, and reasons so pairing decisions can be inspected in the workbook.

- Stable error identity:
  `ClassifiedException` carries correlation ID and exit classification through wrapping and sanitization, including a dedicated canceled exit code.

- Report fidelity:
  previews are report-only; full before/after text remains in domain results and can be written to the `FullText` worksheet when enabled.

- Deterministic reporting:
  workbook output ordering is derived from chapter ordering and stable keys, while freeze panes, autofilters, and diagnostics visibility are preserved across sheet writers.

## Test strategy

The test project now covers:

- config precedence and validation
- exception correlation behavior
- application pipeline concurrency and cancellation exit handling
- chapter segmentation heuristics
- chapter matching anchors and context handling
- diff regressions and shared-block diff behavior
- report workbook structure and report options
- cancellation behavior
- a synthetic PDF-to-workbook integration path
