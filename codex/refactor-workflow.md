---
description: PdfSpecDiffReporter 쿼크 단위 분석 결과를 반영한 멀티 에이전트 리팩토링 워크플로우
---

# Refactoring Workflow — 멀티 에이전트 병렬 실행

## 개요

6개 전문 에이전트가 4단계(Phase)로 실행되어 30+건의 분석 결과를 코드에 반영합니다.

---

## Phase 1: 탐색 & 경량 수정 (병렬)

| Agent | 역할 | 모드 | 예상 시간 |
|-------|------|------|----------|
| `dead-code` | D-1~D-5 데드코드 제거 | Read-Write | ~3분 |
| `security` | S-1~S-4 보안 감사 보고서 | Read-Only | ~2분 |
| `quality` | Q-1~Q-6 품질 개선 | Read-Write | ~3분 |

### 실행 명령
```
아래 Codex CLI 프롬프트를 Phase 1로 실행:

dead-code 에이전트: codex/agents/refactor_dead_code/SKILL.md 파일을 읽고 모든 지침 실행
security 에이전트: codex/agents/refactor_security/SKILL.md 파일을 읽고 보안 감사 보고서 생성
quality 에이전트: codex/agents/refactor_quality/SKILL.md 파일을 읽고 모든 지침 실행
```

### 게이트
```bash
dotnet build --warnaserror  # 0 errors 필수
```

---

## Phase 2: 중량 리팩토링 (병렬)

| Agent | 역할 | 모드 | 예상 시간 |
|-------|------|------|----------|
| `performance` | P-1~P-5 성능 최적화 | Read-Write | ~5분 |
| `architecture` | A-1~A-7 구조 개선 | Read-Write | ~5분 |

### 의존성
- Phase 1의 `dead-code` 에이전트 완료 후 실행 (Levenshtein 통합 필요)
- `security` 보고서 결과 반영

### 게이트
```bash
dotnet build --warnaserror  # 0 errors 필수
```

---

## Phase 3: 테스트 보강 (순차)

| Agent | 역할 | 모드 | 예상 시간 |
|-------|------|------|----------|
| `test-coverage` | T-1~T-7 누락 테스트 작성 | Read-Write | ~5분 |

### 의존성
- Phase 1 + Phase 2 완료 후 (모든 코드 변경 반영됨)

### 게이트
```bash
dotnet test --configuration Release --verbosity normal  # 0 failures 필수
```

---

## Phase 4: 최종 검증 (Orchestrator)

1. `dotnet build --warnaserror` — 빌드 성공 확인
2. `dotnet test --configuration Release --verbosity normal` — 전체 테스트 통과
3. security 에이전트 보고서 검토 (`.codex/reports/security-audit.md`)
4. 변경 파일 목록 및 diff 요약 생성

---

## 전체 시퀀스 다이어그램

```
[Orchestrator] ─┬─→ [dead-code]   ──┐
                ├─→ [security]    ──┤  Phase 1
                └─→ [quality]     ──┘
                         │ build gate
                ┌────────┴────────┐
                ↓                 ↓
         [performance]    [architecture]    Phase 2
                ↓                 ↓
                └────────┬────────┘
                         │ build gate
                         ↓
                  [test-coverage]           Phase 3
                         │ test gate
                         ↓
                  [Orchestrator]            Phase 4 — 최종 검증
```
