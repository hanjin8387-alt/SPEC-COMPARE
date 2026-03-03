---
name: security-audit
description: HC 제약 검증, 예외 살균, NuGet 감사, PdfPig 커스텀 빌드 확인
---

# Security Audit Skill (Read-Only)

## 검증 체크리스트

### HC-1: 외부 AI API 호출 금지
- 전체 .cs 파일에서 `HttpClient`, `WebRequest`, `RestClient`, `HttpWebRequest` 검색
- 예상 결과: 0건 → [PASS]

### HC-2: 런타임 네트워크 접근 금지
- `System.Net`, `Socket`, `TcpClient`, `UdpClient` 사용 여부
- NuGet 복원은 빌드 시만 허용
- 예상 결과: 0건 → [PASS]

### HC-3: 전문 디스크 저장 금지
- `File.WriteAllText`, `File.WriteAllBytes`, `StreamWriter` 검색
- .xlsx 출력 외 디스크 쓰기가 있으면 [FAIL]
- 예상 결과: ExcelReporter만 → [PASS]

### HC-4: 문서 콘텐츠 로깅 금지
- `Console.WriteLine`, `Debug.WriteLine`, `ILogger` 사용 검색
- 문서 내용이 출력되는 경우 [FAIL]
- Spectre.Console MarkupLine은 상태 출력용이므로 콘텐츠 미포함 확인

### HC-5: 예외 메시지 살균
- 모든 catch 블록에서 `ExceptionSanitizer.Sanitize()` 또는 `Wrap()` 사용 확인
- 직접 `ex.Message`를 사용자에게 노출하는 곳 탐지

### 추가 검증
- S-1: `Wrap()` 메서드의 타입 이름 노출 검토
- S-2: Program.cs 경로 출력 검토
- S-3: NuGetAudit=false 플래그
- S-4: PdfPig 1.7.0-custom-5 공급망 위험

## 출력 형식
`.codex/reports/security-audit.md`에 아래 형식으로 저장:
```
# Security Audit Report — {날짜}
## Summary: X PASS / Y FAIL / Z WARNING

| # | Check | Status | File:Line | Severity | Detail |
|---|-------|--------|-----------|----------|--------|
```
