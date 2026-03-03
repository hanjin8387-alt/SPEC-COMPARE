# PdfSpecDiffReporter — Codex CLI 명령어 치트시트

> **Ref:**
> - [Codex CLI](https://developers.openai.com/codex/cli/)
> - [Multi-Agent](https://developers.openai.com/codex/multi-agent)
> - [SDK](https://developers.openai.com/codex/sdk)
> - [Config](https://developers.openai.com/codex/config-basic)

---

## 🚀 초기 설정

```powershell
# 1. Codex CLI 설치
npm i -g @openai/codex

# 2. 프로젝트 폴더로 이동
cd "C:\Users\HJSA\Desktop\개발\Spec 비교"

# 3. 멀티에이전트 활성화 (최초 1회)
codex features enable multi_agent

# 4. 로그인 (최초 1회)
codex
```

---

## 📋 빌드 실행 방법

### 방법 1: 자동 오케스트레이터 (추천)

```powershell
# 전체 빌드 (Phase 0→5)
.\run-codex-build.ps1

# 특정 Phase부터 시작 (예: Phase 2부터)
.\run-codex-build.ps1 -Phase 2

# 드라이런 (명령어 확인만)
.\run-codex-build.ps1 -DryRun

# 빌드 게이트 건너뛰기
.\run-codex-build.ps1 -SkipGate
```

### 방법 2: 인터랙티브 모드

```powershell
# Codex TUI 실행
codex

# TUI 안에서 프롬프트 직접 입력:
# "Read codex/SKILL.md and workflow.md, then execute Phase 0"
```

### 방법 3: 단일 Phase codex exec

```powershell
# Phase 0 — 프로젝트 셋업
codex exec "Read codex/agents/01_project_setup/SKILL.md and execute all steps. Verify with dotnet build --warnaserror."

# Phase 1 — PDF 추출 + 정규화 (멀티에이전트 병렬)
codex exec "Spawn 2 parallel builder agents: Agent1 reads codex/agents/02_pdf_extract/SKILL.md and implements SecureIngestion + TextExtractor. Agent2 reads codex/agents/03_text_cleanup/SKILL.md and implements TextNormalizer. Wait for both and verify build."

# Phase 2 — 챕터 분리 & 매칭
codex exec "Read codex/agents/04_chapter_split_match/SKILL.md. Implement ChapterSegmenter and ChapterMatcher. Verify build."

# Phase 3 — Diff 엔진 + Excel (멀티에이전트 병렬)
codex exec "Spawn 2 parallel builder agents: Agent1 reads codex/agents/05_diff_engine/SKILL.md and implements SimilarityCalculator + DiffEngine. Agent2 reads codex/agents/06_excel_writer/SKILL.md and implements ExcelReporter. Wait and verify."

# Phase 4 — CLI UX + 테스트 + 리뷰 (3 에이전트 병렬)
codex exec "Spawn 3 parallel agents: builder(CLI UX from 07_cli_ux_tests/SKILL.md), tester(write all unit tests), reviewer(scan for HC violations). Wait for all, run dotnet test."

# Phase 5 — 최종 퍼블리시
codex exec "Run dotnet test, then dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true. Report file size."
```

---

## 👁 폴더 워치 (자동 빌드/테스트)

```powershell
# 기본: 빌드 + 테스트
.\watch-codex.ps1

# 빌드만
.\watch-codex.ps1 -BuildOnly

# 빌드 + 테스트 + Codex 리뷰 에이전트
.\watch-codex.ps1 -WithReview

# 디바운스 시간 변경 (기본 2초)
.\watch-codex.ps1 -Debounce 5
```

---

## 🔧 유틸리티 명령어

```powershell
# 모델 변경
codex --model gpt-5.3-codex

# 이전 세션 이어서 작업
codex resume --last

# 특정 세션 재개
codex resume <SESSION_ID>

# 코드 리뷰
codex exec "Review all .cs files for security issues, code quality, and test coverage. Use read-only mode."

# 슬래시 명령 (TUI 안에서)
/model          # 모델 전환
/agent          # 서브 에이전트 관리
/permissions    # 승인 모드 변경
/experimental   # 실험적 기능 토글
/exit           # 세션 종료
```

---

## 🔄 세션 관리 & 디버깅

```powershell
# 최근 세션 목록
codex resume

# 전체 세션 (다른 디렉토리 포함)
codex resume --all

# 이전 세션에서 이어서 codex exec
codex exec resume --last "Fix the build errors and continue Phase 3"

# Feature flag 리스트
codex features list

# 설정 일회성 오버라이드
codex -c model_reasoning_effort=high "Implement the diff engine with very careful reasoning"
codex -c approval_policy=never exec "Run all tests silently"
```

---

## 🎯 최적 워크플로우

```
1. 터미널 1: codex (인터랙티브 TUI)
2. 터미널 2: .\watch-codex.ps1 -WithReview (폴더 워치)
3. 터미널 3: 수동 dotnet build / dotnet test (검증)
```

> **Tip:** TUI에서 Ctrl+G를 누르면 긴 프롬프트를 에디터에서 편집 가능.

---

## 📁 파일 구조

```
Spec 비교/
├── .codex/
│   ├── config.toml              ← 프로젝트 설정 + 에이전트 역할
│   └── agents/
│       ├── explorer.toml        ← 읽기 전용 탐색
│       ├── builder.toml         ← 코드 작성
│       ├── tester.toml          ← 테스트 작성/실행
│       └── reviewer.toml        ← 보안 검증
├── AGENTS.md                    ← Codex 자동 탐색 (프로젝트 가이드)
├── codex/
│   ├── SKILL.md                 ← 마스터 오케스트레이션
│   ├── workflow.md              ← Phase 0→5 빌드 워크플로우
│   └── agents/01~07/SKILL.md    ← 세부 에이전트 스킬
├── run-codex-build.ps1          ← 자동 오케스트레이터 스크립트
├── watch-codex.ps1              ← 폴더 워치 스크립트
└── CODEX-COMMANDS.md            ← 이 파일
```
