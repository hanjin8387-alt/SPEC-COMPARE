---
name: performance-optimization
description: Levenshtein Two-Row 최적화, 불필요 재정규화 제거, FileShare 수정 등
---

# Performance Optimization Skill

## 목표
식별된 5개 성능 병목을 해결하여 대용량 PDF 처리 시 메모리/속도를 개선합니다.

## P-1: Levenshtein Two-Row 최적화

`SimilarityCalculator.cs`의 통합된 `LevenshteinDistance()`를:

```csharp
internal static int LevenshteinDistance(string source, string target)
{
    if (source.Length > target.Length)
        (source, target) = (target, source); // shorter = source

    var prevRow = new int[source.Length + 1];
    var currRow = new int[source.Length + 1];

    for (var i = 0; i <= source.Length; i++)
        prevRow[i] = i;

    for (var j = 1; j <= target.Length; j++)
    {
        currRow[0] = j;
        for (var i = 1; i <= source.Length; i++)
        {
            var cost = source[i - 1] == target[j - 1] ? 0 : 1;
            currRow[i] = Math.Min(
                Math.Min(currRow[i - 1] + 1, prevRow[i] + 1),
                prevRow[i - 1] + cost);
        }
        (prevRow, currRow) = (currRow, prevRow);
    }
    return prevRow[source.Length];
}
```

메모리: O(min(n,m)) vs 기존 O(n×m)

## P-2: 불필요 재정규화 제거

`TextNormalizer.IsSimilar()`:
- 입력이 이미 `Normalize()` 처리된 zone text인 경우가 대부분
- `IsSimilarNormalized()` 내부 메서드 추가: Normalize 없이 직접 비교
- `FindRepeatingZones`, `IsInRepeatedSet`에서 호출 시 이미 정규화된 데이터임을 활용

## P-3: HashSet으로 O(n²) → O(n)

`ChapterMatcher.cs`:
```diff
-var unmatchedSource = source
-    .Where(node => result.Matches.All(match => match.Source?.Key != node.Key))
-    .ToList();
+var matchedSourceKeys = new HashSet<string>(
+    result.Matches.Where(m => m.Source != null).Select(m => m.Source!.Key),
+    StringComparer.Ordinal);
+var unmatchedSource = source.Where(node => !matchedSourceKeys.Contains(node.Key)).ToList();
```

## P-4: LINQ TakeRange → ArraySegment

`DiffEngine.cs`:
```diff
-return values.Skip(start).Take(maxCount).ToList();
+return new List<string>(new ArraySegment<string>(values, start, maxCount));
```

## P-5: FileShare.Read

`SecureIngestion.cs`:
```diff
-FileShare.None
+FileShare.Read
```

## 검증
```bash
dotnet build --warnaserror
dotnet test --configuration Release --verbosity normal
```
