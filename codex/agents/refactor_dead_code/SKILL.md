---
name: dead-code-removal
description: Levenshtein 3중복, SplitLines 2중복, 데드 래퍼 메서드/클래스 탐지 및 제거
---

# Dead Code Removal Skill

## 목표
PdfSpecDiffReporter의 모든 미사용·중복 코드를 제거하여 유지보수성을 높입니다.

## 작업 목록

### 1. 데드 래퍼 삭제

| 파일 | 대상 | 행동 |
|------|------|------|
| `Pipeline/TextExtractor.cs` | `PdfTextExtractor` 클래스 (line 66-72) | 전체 삭제 |
| `Pipeline/TextExtractor.cs` | `Extract()` 메서드 (line 13-16) | 삭제 |
| `Pipeline/SecureIngestion.cs` | `Load()` 메서드 (line 9-12) | 삭제 |

### 2. Levenshtein 통합 (3곳 → 1곳)

**유지:** `Helpers/SimilarityCalculator.cs`의 `LevenshteinDistance()`
- 접근 제한자를 `internal static`으로 변경
- public 메서드 `Calculate(string, string)`에 case-insensitive 옵션 추가

**삭제:**
- `Pipeline/ChapterMatcher.cs:122-150` — `LevenshteinDistance()` 삭제
  - `CalculateSimilarity()` → `SimilarityCalculator.Calculate()` 호출로 대체
- `Pipeline/TextNormalizer.cs:273-301` — `LevenshteinDistance()` 삭제
  - `IsSimilar()` → `SimilarityCalculator.Calculate()` 결과와 threshold 비교

### 3. SplitLines 통합 (2곳 → 1곳)

**방법:** `Helpers/TextUtilities.cs` [NEW] 생성
```csharp
namespace PdfSpecDiffReporter.Helpers;

internal static class TextUtilities
{
    public static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }
}
```

**삭제:**
- `ChapterSegmenter.cs:177-188` → `TextUtilities.SplitLines()` 호출
- `TextNormalizer.cs:379-390` → `TextUtilities.SplitLines()` 호출

## 검증
```bash
dotnet build --warnaserror
dotnet test --configuration Release --verbosity normal
```
