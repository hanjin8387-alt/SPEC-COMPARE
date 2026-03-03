---
name: architecture-improvement
description: 모델 위치 이동, 상수 통합, 가짜 비동기 제거, enum 보완 등 구조 개선
---

# Architecture Improvement Skill

## A-1: 가짜 비동기 제거

`Program.cs`: `StartAsync` 내 `await Task.CompletedTask` 제거
- 모든 파이프라인 작업이 동기이므로 `.Start()` 동기 메서드 사용
- 또는 CPU-bound 작업을 `Task.Run`으로 래핑하여 실제 비동기화

## A-2: GC.Collect() 제거

`Program.cs:269`: `GC.Collect()` 호출 삭제. 프로세스 종료 시 자동 수집.

## A-3: MaxExcerptLength 통합

1. `Models/Constants.cs` [NEW] 생성:
```csharp
namespace PdfSpecDiffReporter.Models;
internal static class Constants
{
    public const int MaxExcerptLength = 500;
}
```
2. `DiffItem.cs`, `ExcelReporter.cs`에서 `Constants.MaxExcerptLength` 참조

## A-4: 도메인 모델 위치 이동

| From | To |
|------|----|
| `ChapterMatcher.cs:153` `ChapterPair` record | `Models/ChapterPair.cs` [NEW] |
| `ChapterMatcher.cs:155-162` `ChapterMatchResult` class | `Models/ChapterMatchResult.cs` [NEW] |

## A-5: ChangeType에 Unchanged 추가

```csharp
public enum ChangeType
{
    Unchanged,
    Added,
    Deleted,
    Modified
}
```

## A-6: Parse/Invoke 통합

`Program.cs`에서 `rootCommand.Parse()` + 에러 체크 로직 제거.
`SetHandler` 내부에서 validation 수행 (이미 구현됨).
직접 `return await rootCommand.InvokeAsync(args);` 만 호출.

## A-7: Correlation ID 길이 통일

`ExceptionSanitizer.Wrap()`:
```diff
-var correlationId = Guid.NewGuid().ToString("N");
+var correlationId = Guid.NewGuid().ToString("N")[..8];
```

## 검증
```bash
dotnet build --warnaserror
dotnet test --configuration Release --verbosity normal
```
