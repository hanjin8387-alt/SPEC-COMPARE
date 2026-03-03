# AGENTS.md — PdfSpecDiffReporter
# Ref: https://developers.openai.com/codex/guides/agents-md/

> **Project:** PdfSpecDiffReporter  
> **Target:** .NET 8 C# CLI (Windows, win-x64 single-file EXE)  
> **Objective:** 두 PDF Spec을 비교하여 챕터 단위 차이점을 Excel(.xlsx)로 리포트

---

## Hard Constraints (절대 위반 금지)

| ID    | 제약                                      |
|-------|------------------------------------------|
| HC-1  | 외부 AI API 호출 금지 (OpenAI, Azure 등)    |
| HC-2  | 런타임 네트워크 접근 금지 (빌드 시 NuGet만 허용) |
| HC-3  | 전문(full-text) 디스크 저장 금지 (최종 .xlsx만 허용, 발췌 ≤ 500 chars) |
| HC-4  | 문서 콘텐츠 로깅 금지 (Console, Debug, ILogger) |
| HC-5  | 예외 메시지 살균 (Correlation ID 방식)       |

## Approved Dependencies

- `UglyToad.PdfPig` (Apache 2.0)
- `ClosedXML` (MIT)
- `DiffPlex` (Apache 2.0)
- `System.CommandLine` (MIT)
- `Spectre.Console` (MIT)

## Project Structure

```
PdfSpecDiffReporter/
├── Models/          # ChapterNode, DiffItem, PageText, WordInfo
├── Pipeline/        # SecureIngestion, TextExtractor, TextNormalizer,
│                    # ChapterSegmenter, ChapterMatcher, DiffEngine
├── Helpers/         # ExcelReporter, ExceptionSanitizer, SimilarityCalculator
└── Program.cs       # CLI entry point (System.CommandLine + Spectre.Console)
```

## Build & Verification

```bash
# 빌드 (경고를 에러로 처리)
dotnet build --warnaserror

# 테스트
dotnet test --configuration Release --verbosity normal

# 게시 (단일 파일 EXE)
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## Agent Skill Files

Codex가 빌드 시 순서대로 실행할 Skill 파일:

1. `codex/agents/01_project_setup/SKILL.md` → 프로젝트 스캐폴딩
2. `codex/agents/02_pdf_extract/SKILL.md` → PDF 로딩 & 텍스트 추출
3. `codex/agents/03_text_cleanup/SKILL.md` → 헤더/푸터 제거 & 정규화
4. `codex/agents/04_chapter_split_match/SKILL.md` → 챕터 분리 & 매칭
5. `codex/agents/05_diff_engine/SKILL.md` → 문단 단위 차이점 비교
6. `codex/agents/06_excel_writer/SKILL.md` → Excel 리포트 생성
7. `codex/agents/07_cli_ux_tests/SKILL.md` → CLI UX & 최종 테스트

## Workflow

전체 빌드 워크플로우: `codex/workflow.md`
마스터 오케스트레이션: `codex/SKILL.md`
