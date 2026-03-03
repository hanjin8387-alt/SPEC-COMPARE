---
name: test-coverage-expansion
description: InputValidator, SecureIngestion, TextExtractor, E2E 통합 등 누락 테스트 작성
---

# Test Coverage Expansion Skill

## 테스트 작성 규칙
- xUnit 사용 (기존 프로젝트 설정)
- 합성(synthetic) 데이터만 사용 — 실제 PDF 불필요
- try/finally로 임시 파일 반드시 정리
- 문서 콘텐츠 로깅 절대 금지 (HC-4)

## 작성 목록

### T-1: SecureIngestionTests.cs [NEW]
```csharp
[Fact] LoadToMemory_WithValidFile_ReturnsMemoryStreamAtPositionZero()
[Fact] LoadToMemory_WithEmptyPath_ThrowsArgumentException()
[Fact] LoadToMemory_WithMissingFile_ThrowsSanitizedException()
[Fact] LoadToMemory_ReleasesFileHandleAfterLoad()
```

### T-2: TextExtractorTests.cs [NEW]
```csharp
[Fact] ExtractPages_WithNull_ThrowsArgumentNullException()
[Fact] ExtractPages_WithEmptyStream_ThrowsOrReturnsEmpty()
```

### T-3: InputValidatorTests.cs [NEW]
```csharp
// ValidatePdfPath
[Theory] ValidatePdfPath_WithInvalidInputs(null, "", "file.txt", "missing.pdf")
[Fact] ValidatePdfPath_WithValidPdf_ReturnsValid()

// ValidateThreshold
[Theory] ValidateThreshold_WithInvalidValues(NaN, Infinity, 0, -1, 2)
[Theory] ValidateThreshold_WithValidValues(0.01, 0.5, 1.0)

// ValidateOptionalConfigPath
[Fact] ValidateOptionalConfigPath_WithNull_ReturnsValid()
[Fact] ValidateOptionalConfigPath_WithWrongExtension_ReturnsInvalid()

// ValidateOutputPath
[Theory] ValidateOutputPath_WithInvalidInputs(null, "", "report.txt")
[Fact] ValidateOutputPath_WithValid_ReturnsResolvedPath()
```

### T-4: ChapterMatcherTests.cs [EXTEND]
```csharp
[Fact] Match_WithDeeplyNestedChildren_FlattensCorrectly()
[Fact] Match_WithLargeInput_CompletesInReasonableTime()
```

### T-5: DiffEngineTests.cs [EXTEND]
```csharp
[Fact] ComputeDiffs_WithEmptyContentChapters_ReturnsNoDiffs()
[Fact] ComputeDiffs_WithThresholdAtBoundary()
```

### T-6: ProgramIntegrationTests.cs [NEW]
```csharp
[Fact] Main_WithHelpFlag_ReturnsZero()
[Fact] Main_WithMissingArgs_ReturnsNonZero()
```

### T-7: ExcelReporterTests.cs [EXTEND]
```csharp
[Fact] Generate_UnmatchedSheet_HasCorrectRowCount()
[Fact] Generate_RowColors_MatchChangeType()
```

## 검증
```bash
dotnet test --configuration Release --verbosity normal
# 0 failures 필수
```
