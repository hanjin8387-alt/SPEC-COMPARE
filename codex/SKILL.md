---
name: pdf-spec-diff-reporter
description: >
  Master orchestration skill for building PdfSpecDiffReporter —
  a .NET 8 Windows CLI EXE that compares two PDF specifications
  and outputs a chaptered diff report in Excel (.xlsx).
  Trigger this skill when setting up the full project or coordinating
  the build pipeline (Phase 0–5).
---

# PdfSpecDiffReporter – Master Orchestration Skill

> **Reference docs (keep these links in context):**
> - Codex Prompting: <https://developers.openai.com/codex/prompting/>
> - Codex Quickstart: <https://developers.openai.com/codex/quickstart/>
> - AGENTS.md Guide: <https://developers.openai.com/codex/guides/agents-md/>
> - Agent Skills: <https://developers.openai.com/codex/skills/>
> - Codex Product Page: <https://openai.com/codex/>

---

## 1  Project Identity

| Field | Value |
|---|---|
| **Name** | `PdfSpecDiffReporter` |
| **Version** | 1.0.0 |
| **Language** | C# 12 |
| **Framework** | .NET 8 (LTS) |
| **Deployment** | Self-Contained SingleFile EXE (`win-x64`) |
| **Target OS** | Windows 10 / 11 x64 |

---

## 2  Read-Before-Start (Mandatory)

Before writing any code, Codex **MUST** read the following files in order:

1. `codex/SKILL.md` ← *this file*
2. `codex/workflow.md` ← Phase 0–5 build workflow
3. `PdfSpecDiffReporter-saved.md` ← Full SPEC_PACK.json + CODEX_BUILD_PROMPT
4. Each agent skill in order:
   - `codex/agents/01_project_setup/SKILL.md`
   - `codex/agents/02_pdf_extract/SKILL.md`
   - `codex/agents/03_text_cleanup/SKILL.md`
   - `codex/agents/04_chapter_split_match/SKILL.md`
   - `codex/agents/05_diff_engine/SKILL.md`
   - `codex/agents/06_excel_writer/SKILL.md`
   - `codex/agents/07_cli_ux_tests/SKILL.md`

---

## 3  Hard Constraints (NEVER Violate)

> [!CAUTION]
> Violation of any constraint below is a **build-failure**.

| ID | Rule | Rationale |
|---|---|---|
| **HC-1** | **No external AI API calls** | Offline tool |
| **HC-2** | **No network access** at runtime | Security / offline |
| **HC-3** | **No full-text persistence** to disk | Only the final `.xlsx` is written; excerpts ≤ 500 chars |
| **HC-4** | **No document-content logging** | Console, Debug, file logs must NEVER contain raw PDF text |
| **HC-5** | **Sanitize all exception messages** | Stack traces may leak content — wrap in `SanitizedException` |

### Sanitization Pattern

```csharp
public static class ExceptionSanitizer
{
    public static string Sanitize(Exception ex)
    {
        // Never expose InnerException.Message directly
        return $"[{ex.GetType().Name}] Operation failed. Correlation={Guid.NewGuid():N}";
    }
}
```

---

## 4  NuGet Dependencies (Approved List)

Only the following packages are permitted:

| Package | Purpose | License |
|---|---|---|
| `UglyToad.PdfPig` | PDF text extraction with coordinates | Apache 2.0 |
| `ClosedXML` | Excel `.xlsx` generation | MIT |
| `DiffPlex` | LCS / diff algorithm | Apache 2.0 |
| `System.CommandLine` | CLI argument parsing | MIT |
| `Spectre.Console` | Progress bar / UX | MIT |

> Adding packages **not listed above** requires explicit justification in a comment.

---

## 5  Build Order (Phase Map)

```
Phase 0  →  Project Scaffolding + deps
Phase 1  →  PDF page-level text extraction (PdfPig)
Phase 2  →  Header/footer cleanup + text normalization
Phase 3  →  Chapter segmentation + Source↔Target matching
Phase 4  →  Diff engine + Excel report generation
Phase 5  →  Hardening (perf, error handling, acceptance tests)
```

Each phase has a dedicated agent skill (`01`–`07`) in `codex/agents/`.
See `codex/workflow.md` for step-by-step instructions per phase.

---

## 6  Verification Gates

After **every phase** the following must pass:

```bash
# Gate 1: Zero-error build
dotnet build --configuration Release --warnaserror

# Gate 2: All unit tests pass
dotnet test --configuration Release --no-build --verbosity normal

# Gate 3: Single-file publish (Phase 0+)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

> **0 errors, 0 warnings** is the target.
> If a warning cannot be suppressed, document it in a `// WARN-OK: <reason>` comment.

---

## 7  Data Contracts

### ChapterNode

```csharp
public sealed class ChapterNode
{
    public string Key { get; init; } = "";          // e.g. "1.2"
    public string Title { get; init; } = "";
    public StringBuilder Content { get; } = new();
    public int PageStart { get; set; }
    public int PageEnd { get; set; }
    public List<ChapterNode> Children { get; } = [];
}
```

### DiffItem

```csharp
public enum ChangeType { Added, Deleted, Modified }

public sealed class DiffItem
{
    public string ChapterKey { get; init; } = "";
    public ChangeType ChangeType { get; init; }
    public string BeforeText { get; init; } = "";   // truncated ≤ 500 chars
    public string AfterText { get; init; } = "";     // truncated ≤ 500 chars
    public double SimilarityScore { get; init; }
    public string PageRef { get; init; } = "";
}
```

---

## 8  Project Layout (Target)

```
PdfSpecDiffReporter/
├── PdfSpecDiffReporter.csproj
├── Program.cs                  # CLI entry point
├── Pipeline/
│   ├── SecureIngestion.cs      # Phase 1
│   ├── TextCleanup.cs          # Phase 2
│   ├── ChapterSegmenter.cs     # Phase 3
│   ├── ChapterMatcher.cs       # Phase 3
│   ├── DiffEngine.cs           # Phase 4
│   └── ExcelReporter.cs        # Phase 4
├── Models/
│   ├── ChapterNode.cs
│   ├── DiffItem.cs
│   └── ChangeType.cs
├── Helpers/
│   ├── ExceptionSanitizer.cs
│   ├── TextNormalizer.cs
│   └── SimilarityCalculator.cs
└── Tests/
    └── PdfSpecDiffReporter.Tests/
        ├── PdfSpecDiffReporter.Tests.csproj
        ├── TextCleanupTests.cs
        ├── ChapterSegmenterTests.cs
        ├── ChapterMatcherTests.cs
        ├── DiffEngineTests.cs
        ├── ExcelReporterTests.cs
        └── SimilarityCalculatorTests.cs
```

---

## 9  Exit Code Convention

| Code | Meaning |
|---|---|
| `0` | Success — `.xlsx` written |
| `1` | Runtime error |
| `2` | Invalid arguments / missing files |

---

## 10  Memory & Stream Policy

1. Load PDF via `FileStream` → copy into `MemoryStream` → **close FileStream immediately**.
2. Process pages sequentially; never hold the entire document text in a single `string`.
3. `StringBuilder` per chapter is acceptable; clear it after diff computation.
4. Only the `ExcelReporter` writes to disk (`ClosedXML` → `FileStream`).
5. On any exception, call `GC.Collect()` in `finally` to minimize residual heap data.
