# AGENTS.md

Repository-specific guidance for future Codex runs in `SPEC-COMPARE`.

## Build and test rules

- Keep restore locked by default.
- Use:
  - `dotnet restore PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj --locked-mode`
  - `dotnet build PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj -c Release --no-restore --warnaserror`
  - `dotnet test PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage"`
- If package references change, update `packages.lock.json` intentionally with `-p:RestoreLockedMode=false`, then re-run locked restore to verify.

## Runtime and portability rules

- Do not reintroduce `RuntimeIdentifier`, `PublishSingleFile`, or `SelfContained` defaults into `PdfSpecDiffReporter.csproj`.
- Keep default output paths cross-platform.
- CI must continue to verify on both Windows and Linux.
- Do not add publish steps back to pull-request verification.

## Code review focus

- `Program.cs` should stay a composition root only.
- Pipeline entry points should keep accepting `CancellationToken`.
- Core models should preserve full diff text; truncation belongs only in reporting.
- Matching changes must preserve or improve explainability through `ChapterMatchEvidence`.
- Prefer simpler code over additional abstraction. Remove stale helpers and packages instead of keeping half-used layers.

## Reporting rules

- Workbook changes must preserve:
  - frozen header rows
  - autofilter-enabled data sheets
  - deterministic ordering
  - full-text access
  - diagnostics visibility

## Test expectations

- Add or update deterministic tests for matcher and diff regressions whenever scoring or block-building logic changes.
- Maintain at least one synthetic PDF-to-report integration path.
