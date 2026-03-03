# PdfSpecDiffReporter — Saved Specification & Build Prompt

> Generated: 2026-02-19
> Version: 1.0.0

---

## Part 1: SPEC_PACK.json (Original)

```json
{
  "meta": {
    "project_name": "PdfSpecDiffReporter",
    "version": "1.0.0",
    "description": "Secure, offline CLI tool for structural comparison of PDF specifications.",
    "stack": {
      "language": "C#",
      "framework": ".NET 8",
      "deployment": "Self-Contained SingleFile EXE",
      "target_os": "Windows 10/11 x64"
    },
    "libraries": {
      "pdf_processing": "PdfPig (Apache 2.0) or iTextSharp (LGPL fallback)",
      "diff_algorithm": "DiffPlex or Custom LCS",
      "excel_export": "ClosedXML",
      "cli_parser": "System.CommandLine"
    }
  },
  "constraints": {
    "no_external_ai_api": true,
    "no_network_access": true,
    "no_full_text_persistence": true,
    "never_log_document_content": true,
    "memory_policy": "Volatile Memory Only (Stream processing)",
    "error_handling": "Sanitized Logging (No content leakage)"
  },
  "inputs": {
    "arguments": [
      {
        "name": "source_pdf",
        "type": "string (path)",
        "required": true
      },
      {
        "name": "target_pdf",
        "type": "string (path)",
        "required": true
      }
    ],
    "options": [
      {
        "name": "--output",
        "alias": "-o",
        "default": ".\\diff_report.xlsx"
      },
      {
        "name": "--config",
        "alias": "-c",
        "description": "Path to external JSON config"
      },
      {
        "name": "--threshold",
        "default": 0.85,
        "description": "Similarity threshold for modification detection"
      }
    ]
  },
  "pipeline": [
    {
      "step": 1,
      "name": "Secure Ingestion",
      "action": "Load PDF FileStream into MemoryStream, release file handle immediately."
    },
    {
      "step": 2,
      "name": "Structure Parsing & Cleanup",
      "action": "Extract text with coordinates; Detect/Remove repetitive headers/footers (Top/Bottom 10%); Parse TOC or Regex."
    },
    {
      "step": 3,
      "name": "Chapter Alignment",
      "action": "Match Chapter Nodes between Source and Target based on Key (Number) and Title Similarity."
    },
    {
      "step": 4,
      "name": "Diff Engine",
      "action": "Compute LCS on paragraphs within matched chapters; Classify as Modified (>=0.85) or Replaced (<0.85)."
    },
    {
      "step": 5,
      "name": "Reporting",
      "action": "Generate Excel sheets (Summary, Details, Unmatched) using ClosedXML directly from memory."
    }
  ],
  "data_contracts": {
    "ChapterNode": {
      "key": "string (e.g., '1.2')",
      "title": "string",
      "content": "StringBuilder",
      "page_start": "int",
      "page_end": "int",
      "children": "List<ChapterNode>"
    },
    "DiffItem": {
      "chapter_key": "string",
      "change_type": "enum (ADDED, DELETED, MODIFIED)",
      "before_text": "string (truncated)",
      "after_text": "string (truncated)",
      "similarity_score": "double",
      "page_ref": "string"
    }
  },
  "chapter_detection": {
    "strategy": "Hybrid (TOC Priority -> Regex Fallback)",
    "default_regex": "^(\\d+(\\.\\d+)*)\\s+|^SECTION\\s+\\d+|^CHAPTER\\s+\\d+",
    "scan_scope": "Full Text if TOC missing",
    "hierarchy_logic": "Stack-based parent identification based on heading depth"
  },
  "diff_engine": {
    "granularity": "PARAGRAPH",
    "algorithms": ["LCS (Longest Common Subsequence)", "Levenshtein Distance"],
    "similarity_threshold": 0.85,
    "normalization": "Trim whitespace, ignore case, normalize line endings"
  },
  "excel_output": {
    "filename_pattern": "Diff_Report_{Timestamp}.xlsx",
    "sheets": [
      {
        "name": "Summary",
        "columns": ["Source File", "Target File", "Total Chapters", "Matched", "Modified", "Added", "Deleted", "Processing Time"]
      },
      {
        "name": "ChangeDetails",
        "columns": ["Chapter ID", "Section Title", "Change Type", "Before (Old)", "After (New)", "Similarity (%)", "Page Refs"]
      },
      {
        "name": "Unmatched",
        "columns": ["Origin (Old/New)", "Chapter ID", "Title", "Status"]
      }
    ],
    "formatting": {
      "truncate_limit": 500,
      "wrap_text": true,
      "color_code_changes": true
    }
  },
  "cli_spec": {
    "executable": "PdfSpecDiffReporter.exe",
    "help_flag": "--help",
    "ux_features": [
      "Progress Bar (Spectre.Console)",
      "Phase Status Messages",
      "Exit Codes (0=Success, 1=Error, 2=InvalidArgs)"
    ]
  },
  "backlog": [
    {
      "id": "T1-1",
      "priority": "P0",
      "task": "Project Scaffolding & Single-file Publish Setup",
      "dod": "Hello World exe builds as single file"
    },
    {
      "id": "T2-1",
      "priority": "P0",
      "task": "Secure In-Memory PDF Loader",
      "dod": "FileStream closed immediately after load"
    },
    {
      "id": "T2-3",
      "priority": "P0",
      "task": "Noise Cancellation Logic (Header/Footer)",
      "dod": "95% detection of repeated artifacts"
    },
    {
      "id": "T3-1",
      "priority": "P0",
      "task": "Chapter Segmentation (TOC & Regex)",
      "dod": "Tree structure generated from flat text"
    },
    {
      "id": "T4-1",
      "priority": "P0",
      "task": "Excel Generator Integration",
      "dod": "Valid .xlsx output with 3 sheets"
    }
  ],
  "acceptance_tests": [
    {
      "id": "AT-01",
      "type": "Security",
      "scenario": "Network Isolation",
      "expected_result": "Zero outbound packets during execution."
    },
    {
      "id": "AT-02",
      "type": "Security",
      "scenario": "Data Residue",
      "expected_result": "No temp files (.txt, .tmp) remaining on disk after execution."
    },
    {
      "id": "AT-03",
      "type": "Performance",
      "scenario": "Large File Handling",
      "expected_result": "Compare two 500-page PDFs in under 60 seconds."
    },
    {
      "id": "AT-04",
      "type": "Functional",
      "scenario": "Chapter Mapping",
      "expected_result": "Correctly identifies modification in 'Chapter 2.1' vs insertion of new 'Chapter 3'."
    }
  ]
}
```

---

## Part 2: CODEX_BUILD_PROMPT

```
You are building PdfSpecDiffReporter, a .NET 8 C# CLI application that compares
two PDF specification documents and produces a structured Excel diff report.

═══════════════════════════════════════════════════════════════════════
STEP 0 — READ CONTEXT FILES (MANDATORY, DO NOT SKIP)
═══════════════════════════════════════════════════════════════════════

Before writing ANY code, read these files IN ORDER:

1. codex/SKILL.md           — Master orchestration (constraints, data contracts, project layout)
2. codex/workflow.md         — Phase 0–5 build workflow with verification gates
3. PdfSpecDiffReporter-saved.md — This file (SPEC_PACK.json for reference)

After reading the above, proceed through agent skills in order:

4. codex/agents/01_project_setup/SKILL.md
5. codex/agents/02_pdf_extract/SKILL.md
6. codex/agents/03_text_cleanup/SKILL.md
7. codex/agents/04_chapter_split_match/SKILL.md
8. codex/agents/05_diff_engine/SKILL.md
9. codex/agents/06_excel_writer/SKILL.md
10. codex/agents/07_cli_ux_tests/SKILL.md

═══════════════════════════════════════════════════════════════════════
HARD CONSTRAINTS (ABSOLUTE — VIOLATION = BUILD FAILURE)
═══════════════════════════════════════════════════════════════════════

HC-1: NO external AI API calls (no OpenAI, no Anthropic, no anything)
HC-2: NO network access at runtime (fully offline)
HC-3: NO full-text persistence to disk (only final .xlsx; excerpts ≤ 500 chars)
HC-4: NO document-content logging (Console, Debug, file logs — NEVER raw text)
HC-5: ALL exception messages MUST be sanitized (use ExceptionSanitizer)

═══════════════════════════════════════════════════════════════════════
BUILD ORDER — Execute agents 01 → 07 strictly in sequence
═══════════════════════════════════════════════════════════════════════

PHASE 0 (Agent 01): Project Scaffolding
  - Create .NET 8 solution: PdfSpecDiffReporter + PdfSpecDiffReporter.Tests
  - Add approved NuGet packages: UglyToad.PdfPig, ClosedXML, DiffPlex,
    System.CommandLine, Spectre.Console, Moq, FluentAssertions
  - Configure single-file publish (win-x64, self-contained)
  - Create directory structure: Pipeline/, Models/, Helpers/
  - Implement CLI skeleton with System.CommandLine
  - Create model stubs (ChapterNode, DiffItem, ChangeType)
  - Create ExceptionSanitizer
  - GATE: dotnet build --warnaserror → 0 errors

PHASE 1 (Agent 02): PDF Text Extraction
  - Implement SecureIngestion.cs (FileStream → MemoryStream → close handle)
  - Implement PdfTextExtractor using PdfPig (per-page, with coordinates)
  - Create PageText and WordInfo models
  - Write unit tests (file handle closure, stream content)
  - GATE: dotnet build --warnaserror && dotnet test

PHASE 2 (Agent 03): Text Cleanup
  - Implement TextCleanup.cs (header/footer detection using Y-coordinate zones)
  - Implement TextNormalizer.cs (whitespace, line endings, Unicode NFC)
  - Write unit tests (repeated header detection, normalization edge cases)
  - GATE: dotnet build --warnaserror && dotnet test

PHASE 3 (Agent 04): Chapter Segmentation & Matching
  - Implement ChapterSegmenter.cs (regex-based heading detection, stack hierarchy)
  - Implement ChapterMatcher.cs (exact key match → fuzzy title match → unmatched)
  - Implement SimilarityCalculator.cs (Levenshtein normalized to [0,1])
  - Write unit tests (hierarchy, matching, edge cases)
  - GATE: dotnet build --warnaserror && dotnet test

PHASE 4 (Agents 05 + 06): Diff Engine & Excel Export
  - Implement DiffEngine.cs (paragraph-level diff, change classification)
  - Implement ExcelReporter.cs using ClosedXML (Summary, ChangeDetails, Unmatched sheets)
  - Color-code rows: green=Added, red=Deleted, yellow=Modified
  - Truncate all text excerpts to ≤ 500 characters
  - Write unit tests (diff classification, Excel file structure, color coding)
  - GATE: dotnet build --warnaserror && dotnet test

PHASE 5 (Agent 07): Hardening
  - Wire full CLI pipeline in Program.cs with Spectre.Console progress bar
  - Finalize ExceptionSanitizer (pattern-matched, correlation IDs)
  - Add InputValidator for file path checks
  - Implement exit codes: 0=success, 1=error, 2=invalid args
  - Performance: Stopwatch per phase
  - Verify acceptance tests AT-01 through AT-04
  - GATE: dotnet build -c Release --warnaserror && dotnet test -c Release

═══════════════════════════════════════════════════════════════════════
FINAL PUBLISH
═══════════════════════════════════════════════════════════════════════

dotnet publish -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true

The resulting EXE should:
  ✓ Run with: PdfSpecDiffReporter.exe source.pdf target.pdf -o report.xlsx
  ✓ Show progress bar during processing
  ✓ Output a valid .xlsx with 3 sheets
  ✓ Exit with code 0 on success
  ✓ Leave zero temp files on disk
  ✓ Make zero network calls

═══════════════════════════════════════════════════════════════════════
QUALITY TARGETS
═══════════════════════════════════════════════════════════════════════

  ✓ dotnet build → 0 errors, 0 warnings
  ✓ dotnet test  → all pass
  ✓ No document content in any log output
  ✓ All exceptions sanitized
  ✓ Single-file EXE under 50 MB

═══════════════════════════════════════════════════════════════════════
REFERENCE DOCUMENTATION
═══════════════════════════════════════════════════════════════════════

  Codex Prompting:       https://developers.openai.com/codex/prompting/
  Codex Quickstart:      https://developers.openai.com/codex/quickstart/
  AGENTS.md Guide:       https://developers.openai.com/codex/guides/agents-md/
  Agent Skills:          https://developers.openai.com/codex/skills/
  Codex Product Page:    https://openai.com/codex/
```
