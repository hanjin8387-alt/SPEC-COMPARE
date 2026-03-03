# PdfSpecDiffReporter 소개 및 사용설명서

## 1. 문서 목적
이 문서는 `PdfSpecDiffReporter`의 기능, 실행 방법, 결과 해석, 운영 시 주의사항을 상세히 설명합니다.  
처음 사용하는 사용자부터 운영 담당자까지 바로 활용할 수 있도록 실제 구현 기준으로 작성되었습니다.

## 2. 애플리케이션 개요
`PdfSpecDiffReporter`는 두 개의 PDF 사양서(예: 구버전/신버전)를 비교해, 장(Chapter) 단위 변경사항을 Excel(`.xlsx`) 보고서로 생성하는 Windows CLI 도구입니다.

핵심 목적은 다음과 같습니다.

- 문서 변경점의 구조화
- Added/Deleted/Modified 분류 자동화
- 추적 가능한 보고서 출력
- 민감정보 노출 최소화(예외/로그 보호)

## 3. 대상 환경

- OS: Windows 10/11 x64
- 런타임: .NET 8 기반 빌드 산출물
- 실행 형태: 콘솔(터미널) 앱

중요:

- GUI 창 프로그램이 아닙니다.
- EXE를 더블클릭하면 콘솔이 잠깐 열렸다 닫혀서 “아무 것도 안 켜지는 것처럼” 보일 수 있습니다.
- PowerShell/CMD에서 실행하거나, 제공된 `.bat` 런처를 사용하세요.

## 4. 현재 배포 파일 위치

- EXE:
`PdfSpecDiffReporter/bin/Release/net8.0/win-x64/publish/PdfSpecDiffReporter.exe`
- 배치 런처:
`PdfSpecDiffReporter/bin/Release/net8.0/win-x64/publish/run-pdf-diff.bat`

## 5. 주요 기능

- PDF 텍스트 추출:
페이지별 텍스트와 단어 좌표(X, Y, 글자 크기) 추출
- 텍스트 정리:
헤더/푸터 반복 제거, 페이지 번호 제거, 공백/줄바꿈/유니코드 정규화
- 장 분할:
번호형 제목(`1`, `1.2`, `1.2.3`) 및 `SECTION/CHAPTER` 패턴 인식
- 장 매칭:
키 일치 우선 + 제목 유사도 기반 매칭
- Diff 계산:
문단 단위 비교 후 Added/Deleted/Modified 분류
- Excel 보고서:
`Summary`, `ChangeDetails`, `Unmatched` 3개 시트 생성
- 오류 보호:
예외 메시지 Sanitization 및 참조 ID(Ref/Correlation) 제공

## 6. 처리 파이프라인(실행 시 표시되는 7단계)
실행 중 콘솔에 Spectre.Console 진행바로 아래 7개 단계가 표시됩니다.

1. Secure ingestion  
2. Extracting page text  
3. Cleaning and normalizing text  
4. Segmenting chapters  
5. Matching chapters  
6. Computing diffs  
7. Generating Excel report

## 7. CLI 사용법

기본 문법:

```powershell
PdfSpecDiffReporter.exe <source_pdf> <target_pdf> [options]
```

예시:

```powershell
.\PdfSpecDiffReporter.exe "C:\docs\Spec_v1.pdf" "C:\docs\Spec_v2.pdf" -o "C:\docs\diff_report.xlsx"
```

### 7.1 인자 및 옵션

- 필수 인자:
`source_pdf` 원본 PDF 경로
- 필수 인자:
`target_pdf` 대상 PDF 경로
- 선택 옵션:
`--output`, `-o` 출력 xlsx 경로
기본값: `.\diff_report.xlsx`
- 선택 옵션:
`--config`, `-c` JSON 설정 경로
현재 버전에서는 경로 유효성 검증만 수행
- 선택 옵션:
`--threshold` 유사도 임계값
기본값: `0.85`
허용범위: `0 < threshold <= 1`

### 7.2 도움말

```powershell
.\PdfSpecDiffReporter.exe --help
```

## 8. 배치(.bat) 런처 사용법

실행 파일 옆의 `run-pdf-diff.bat`를 더블클릭하면 대화형으로 입력받아 실행합니다.

- Source PDF full path 입력
- Target PDF full path 입력
- Output .xlsx path 입력
빈 값 입력 시 기본 출력: `publish\diff_report.xlsx`

직접 인자 전달도 가능:

```bat
run-pdf-diff.bat "C:\docs\Spec_v1.pdf" "C:\docs\Spec_v2.pdf" "C:\docs\diff.xlsx"
```

## 9. 입력 검증 규칙

### 9.1 PDF 경로

- 경로가 비어 있으면 실패
- `.pdf` 확장자 필수
- 파일 존재 필수
- 읽기 가능해야 함

### 9.2 출력 경로

- 경로가 비어 있으면 실패
- `.xlsx` 확장자 필수
- 디렉터리 자동 생성 시도
- 경로가 너무 길거나 접근 불가면 실패
- `{Timestamp}` 토큰 자동 치환 지원
예: `C:\out\diff_{Timestamp}.xlsx`

### 9.3 임계값

- 숫자여야 함
- `0`보다 크고 `1` 이하여야 함

## 10. 비교 로직 상세

### 10.1 텍스트 추출

- PDF를 `MemoryStream`으로 로드한 뒤 원본 파일 핸들 즉시 해제
- 페이지별로 `PageText` 생성
- 단어별 좌표(`WordInfo`) 보관

### 10.2 텍스트 정리

- 페이지 상단 10%/하단 10% 영역 후보 추출
- 3페이지 이상 연속 반복 + 유사도 기준 충족 시 헤더/푸터 제거
- 상/하단 페이지 번호 패턴 제거
- 정규화:
유니코드 NFC, 제어문자 제거, 공백 정리, 줄바꿈 통일

### 10.3 장(Chapter) 분할

인식 패턴 예:

- `1 Introduction`
- `1.2 Scope`
- `SECTION 3: Security`
- `CHAPTER 4 Methods`

계층 구조:

- `1.2.3`은 `1.2`의 하위, `1.2`는 `1`의 하위로 처리
- 중복 키 발견 시 `_dupN` 접미사 부여

### 10.4 장 매칭

1차:
키 정확 일치 매칭  
2차:
미매칭 노드끼리 제목 유사도 기반 매칭(기본 임계값 `0.7`)

미매칭은 다음으로 유지:

- `UnmatchedSource`(삭제 후보)
- `UnmatchedTarget`(추가 후보)

### 10.5 Diff 분류

문단 단위 비교 후 분류:

- Added: 대상에만 존재
- Deleted: 원본에만 존재
- Modified: 양쪽 문단 존재 + 유사도 `>= threshold`

유사도 `< threshold`인 경우:

- Modified로 묶지 않고 Deleted + Added로 분리

민감정보 최소화를 위해 결과 텍스트는 최대 500자까지만 보관/출력됩니다.

## 11. Excel 보고서 구조

### 11.1 Summary 시트

컬럼:

- `Source File`
- `Target File`
- `Total Chapters`
- `Matched`
- `Modified`
- `Added`
- `Deleted`
- `Processing Time`

### 11.2 ChangeDetails 시트

컬럼:

- `Chapter ID`
- `Section Title`
- `Change Type`
- `Before (Old)`
- `After (New)`
- `Similarity (%)`
- `Page Refs`

색상 규칙:

- Added: `#C6EFCE` (연녹색)
- Deleted: `#FFC7CE` (연적색)
- Modified: `#FFEB9C` (연노랑)

형식:

- 긴 텍스트 줄바꿈 적용
- 컬럼 자동폭 조정
- 텍스트는 최대 500자

### 11.3 Unmatched 시트

컬럼:

- `Origin (Old/New)`
- `Chapter ID`
- `Title`
- `Status`

값 예시:

- Old / Deleted (No Match in New)
- New / Added (No Match in Old)

## 12. 종료 코드(Exit Code)

- `0`: 성공
- `1`: 런타임 오류
- `2`: 입력 검증 오류
- `3`: 파일/경로 I/O 오류

운영 자동화(배치/스케줄러)에서는 이 코드를 기준으로 재시도/알림 정책을 구성하세요.

## 13. 보안 및 데이터 보호 원칙

이 도구는 다음 원칙을 따릅니다.

- 외부 AI API 호출 없음
- 실행 중 네트워크 의존 동작 없음
- PDF 원문 전체를 디스크에 저장하지 않음
- 콘솔/로그에 문서 본문 노출 금지
- 예외 메시지는 Sanitized 형식으로만 출력

예외 출력 예:

- `File access error. [Ref: 12ab34cd]`
- `Unexpected runtime error (InvalidOperationException). [Ref: 56ef78gh]`

## 14. 자주 겪는 문제와 해결

### 14.1 “아무것도 안 켜짐”

원인:
CLI 앱을 더블클릭 실행  
해결:
PowerShell에서 실행하거나 `run-pdf-diff.bat` 사용

### 14.2 “Validation error: Required argument missing”

원인:
필수 인자 2개(source/target) 누락  
해결:
PDF 2개 경로를 반드시 전달

### 14.3 “Source/Target PDF was not found”

원인:
경로 오타, 접근 불가, 따옴표 누락  
해결:
절대경로 사용, 경로에 공백 있으면 반드시 `"..."`로 감싸기

### 14.4 “Output file must use .xlsx extension”

원인:
출력 파일 확장자 오류  
해결:
반드시 `.xlsx` 사용

### 14.5 한글/공백 경로에서 실행 문제

해결:
경로 전체를 따옴표로 감싸서 전달

## 15. 권장 실행 예시 모음

### 15.1 기본 실행

```powershell
.\PdfSpecDiffReporter.exe "C:\spec\old.pdf" "C:\spec\new.pdf"
```

### 15.2 출력 파일 지정

```powershell
.\PdfSpecDiffReporter.exe "C:\spec\old.pdf" "C:\spec\new.pdf" -o "C:\out\spec_diff.xlsx"
```

### 15.3 임계값 조정

```powershell
.\PdfSpecDiffReporter.exe "C:\spec\old.pdf" "C:\spec\new.pdf" --threshold 0.90
```

### 15.4 타임스탬프 출력

```powershell
.\PdfSpecDiffReporter.exe "C:\spec\old.pdf" "C:\spec\new.pdf" -o "C:\out\diff_{Timestamp}.xlsx"
```

## 16. 운영 팁

- 대용량 문서 비교 시:
로컬 SSD 경로를 사용하면 I/O 대기 시간이 줄어듭니다.
- 임계값 튜닝:
`0.85`는 보수적입니다.
수정 탐지를 더 민감하게 하려면 `0.7~0.8` 구간을 시도하세요.
- 배치 자동화:
종료코드 기반으로 실패 재시도 정책을 두세요.

## 17. 변경/확장 예정 시 참고

현재 `--config`는 경로 검증만 수행합니다.
향후 확장 시 다음을 `config`로 외부화할 수 있습니다.

- 헤더/푸터 제거 파라미터
- 장 인식 정규식
- 임계값 프로파일(문서 유형별)
- Excel 스타일/컬럼 정책

## 18. 빠른 체크리스트

- EXE 위치 확인
- 입력 PDF 2개 절대경로 준비
- 출력 `.xlsx` 경로 지정
- 필요 시 `.bat` 런처 사용
- 실행 후 `Summary`/`ChangeDetails`/`Unmatched` 3시트 확인

---

문서 버전: `v1.0`  
대상 애플리케이션: `PdfSpecDiffReporter`  
작성 기준: 현재 소스 구현(`Program.cs`, `InputValidator.cs`, `ExceptionSanitizer.cs`, `ExcelReporter.cs`) 기준
