# SPEC-COMPARE (PdfSpecDiffReporter)

> 두 PDF 사양서를 비교하여 챕터 단위 차이점을 Excel(.xlsx) 보고서로 생성하는 Windows CLI 도구

## ✨ 주요 기능

- **PDF 텍스트 추출** — 페이지별 텍스트 + 단어 좌표(X, Y, FontSize)
- **텍스트 정리** — 헤더/푸터 자동 제거, 정규화 (Unicode NFC, 공백, 줄바꿈)
- **챕터 분할** — 번호형 제목(`1.2.3`) 및 `SECTION/CHAPTER` 패턴 인식
- **챕터 매칭** — 키 정확일치 → 제목 유사도 기반 매칭
- **Diff 계산** — 문단 단위 Added / Deleted / Modified 분류
- **Excel 보고서** — Summary, ChangeDetails, Unmatched 3시트 (색상 코딩)
- **보안** — 외부 API 호출 없음, 네트워크 없음, 문서 전문 저장 없음, 예외 Sanitization

## 📋 요구 사항

- Windows 10/11 x64
- .NET 8.0 Runtime

## 🚀 사용법

```powershell
# 기본 실행
.\PdfSpecDiffReporter.exe "old_spec.pdf" "new_spec.pdf"

# 출력 경로 지정
.\PdfSpecDiffReporter.exe "old.pdf" "new.pdf" -o "C:\output\diff_report.xlsx"

# 유사도 임계값 조정 (기본 0.85)
.\PdfSpecDiffReporter.exe "old.pdf" "new.pdf" --threshold 0.70

# 타임스탬프 자동 삽입
.\PdfSpecDiffReporter.exe "old.pdf" "new.pdf" -o "diff_{Timestamp}.xlsx"

# 도움말
.\PdfSpecDiffReporter.exe --help
```

또는 `run-pdf-diff.bat`를 더블클릭하여 대화형으로 실행할 수 있습니다.

## 📊 Excel 보고서 구조

| 시트 | 내용 |
|------|------|
| **Summary** | Source/Target 파일, 전체 챕터 수, 매칭/수정/추가/삭제 건수, 처리 시간 |
| **ChangeDetails** | 챕터별 변경상세 (Before/After, 유사도%, 페이지 참조, 색상 코딩) |
| **Unmatched** | 매칭 실패 챕터 (삭제 후보 / 새로 추가된 챕터) |

색상: 🟢 Added(`#C6EFCE`) · 🔴 Deleted(`#FFC7CE`) · 🟡 Modified(`#FFEB9C`)

## 🔧 빌드

```powershell
dotnet build --warnaserror
dotnet test --configuration Release
dotnet publish PdfSpecDiffReporter/PdfSpecDiffReporter.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## 📁 프로젝트 구조

```
├── PdfSpecDiffReporter/
│   ├── Models/          # ChapterNode, DiffItem, PageText, WordInfo 등
│   ├── Pipeline/        # SecureIngestion, TextExtractor, TextNormalizer,
│   │                    # ChapterSegmenter, ChapterMatcher, DiffEngine
│   ├── Helpers/         # ExcelReporter, ExceptionSanitizer, SimilarityCalculator
│   └── Program.cs       # CLI 진입점
├── PdfSpecDiffReporter.Tests/   # xUnit 유닛 테스트
├── codex/               # Codex 멀티에이전트 빌드 스킬
└── .codex/              # Codex CLI 설정 및 에이전트 역할
```

## 🔒 보안 원칙

- ❌ 외부 AI API 호출 없음
- ❌ 런타임 네트워크 접근 없음
- ❌ PDF 원문 디스크 저장 없음 (최종 xlsx만, 발췌 ≤ 500자)
- ❌ 콘솔/로그에 문서 본문 노출 없음
- ✅ 예외 메시지 Sanitization (Correlation ID 방식)

## 📦 Dependencies

- [UglyToad.PdfPig](https://github.com/UglyToad/PdfPig) — PDF 텍스트 추출
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) — Excel 생성
- [DiffPlex](https://github.com/mmanela/diffplex) — 텍스트 Diff
- [System.CommandLine](https://github.com/dotnet/command-line-api) — CLI 파싱
- [Spectre.Console](https://spectreconsole.net/) — 콘솔 UI/진행바

## 📜 License

MIT
