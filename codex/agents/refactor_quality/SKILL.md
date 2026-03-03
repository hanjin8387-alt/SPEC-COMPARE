---
name: code-quality-improvement
description: 일관성, 줄 끝 통일, GC.Collect 제거, DateTime.UtcNow 전환 등
---

# Code Quality Improvement Skill

## Q-1: List.Contains → bool 플래그

`ChapterSegmenter.cs`:
```diff
 ChapterNode? preamble = null;
+bool preambleAdded = false;

-if (!roots.Contains(preamble))
-{
-    roots.Insert(0, preamble);
-}
+if (!preambleAdded)
+{
+    roots.Insert(0, preamble);
+    preambleAdded = true;
+}
```

## Q-2: 빈 컬렉션 생성 통일

규칙:
- 반환 타입 `List<T>` → `new List<T>()`
- 반환 타입 `IReadOnlyList<T>` → `Array.Empty<T>()`
- 적용: `TextNormalizer.BuildZones`, `ChapterSegmenter.SplitLines` 등

## Q-3: PageRange 로직 분리 (선택)

`ChapterNode.PageRange` getter → `ChapterNodeExtensions.GetPageRange()` 확장 메서드로 이동.
모델 순수성 유지.

## Q-4: 기본 threshold 문서화

`DiffEngine.ComputeDiffs`에 XML doc 추가:
```csharp
/// <param name="similarityThreshold">
/// 유사도 임계값. 기본값 0.3은 API 직접 호출 시 사용.
/// CLI의 --threshold(기본 0.85)와 별개이며, Program.cs에서 전달됨.
/// </param>
```

## Q-5: DateTime.UtcNow

`InputValidator.cs:89`:
```diff
-var expandedPath = outputPath.Replace("{Timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"), ...);
+var expandedPath = outputPath.Replace("{Timestamp}", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"), ...);
```

## Q-6: .editorconfig 생성

프로젝트 루트에 `.editorconfig` [NEW]:
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4
```

## 검증
```bash
dotnet build --warnaserror
dotnet test --configuration Release --verbosity normal
```
