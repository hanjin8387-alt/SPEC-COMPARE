<#
╔══════════════════════════════════════════════════════════════════════╗
║  PdfSpecDiffReporter — 폴더 워치 + Codex 자동 빌드/테스트           ║
║  Ref: https://developers.openai.com/codex/cli/features             ║
║  .cs 파일 변경 감지 시 자동으로 codex exec로 빌드 & 테스트 실행     ║
╚══════════════════════════════════════════════════════════════════════╝

사용법:
  .\watch-codex.ps1                    # 기본 (빌드+테스트)
  .\watch-codex.ps1 -BuildOnly        # 빌드만 실행
  .\watch-codex.ps1 -WithReview       # 변경마다 리뷰 에이전트도 실행
  .\watch-codex.ps1 -Debounce 3       # 디바운스 시간 변경 (초)
#>

param(
    [switch]$BuildOnly,
    [switch]$WithReview,
    [int]$Debounce = 2
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WatchPaths = @(
    (Join-Path $ProjectDir "PdfSpecDiffReporter"),
    (Join-Path $ProjectDir "PdfSpecDiffReporter.Tests")
)

# ── 색상 헬퍼 ──────────────────────────────────────────────────────
function Write-Watch($msg) { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] 👁 $msg" -ForegroundColor Cyan }
function Write-Pass($msg)  { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✓ $msg" -ForegroundColor Green }
function Write-Fail($msg)  { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ✖ $msg" -ForegroundColor Red }
function Write-Info($msg)  { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] ℹ $msg" -ForegroundColor Yellow }

# ── 빌드 실행 ──────────────────────────────────────────────────────
function Invoke-Build {
    Write-Watch "Building..."
    Push-Location $ProjectDir
    try {
        $result = dotnet build --warnaserror 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Pass "BUILD PASSED"
        } else {
            Write-Fail "BUILD FAILED"
            $result | Select-Object -Last 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
        }
        return $LASTEXITCODE
    } finally { Pop-Location }
}

# ── 테스트 실행 ────────────────────────────────────────────────────
function Invoke-Tests {
    Write-Watch "Testing..."
    Push-Location $ProjectDir
    try {
        $result = dotnet test --configuration Release --verbosity minimal 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Pass "ALL TESTS PASSED"
        } else {
            Write-Fail "TESTS FAILED"
            $result | Select-Object -Last 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
        }
        return $LASTEXITCODE
    } finally { Pop-Location }
}

# ── Codex 리뷰 에이전트 실행 ──────────────────────────────────────
function Invoke-CodexReview {
    Write-Watch "Running Codex review agent..."
    codex exec "Spawn a reviewer agent. Scan all .cs files that were recently modified for HC-1~HC-5 constraint violations. Report a structured pass/fail checklist. Focus on: no network calls, no full-text persistence, no content logging, sanitized exceptions."
    if ($LASTEXITCODE -eq 0) {
        Write-Pass "REVIEW COMPLETE"
    } else {
        Write-Fail "REVIEW FAILED"
    }
}

# ── Codex 자동 수정 에이전트 ──────────────────────────────────────
function Invoke-CodexAutofix {
    param([string]$ChangedFile)
    Write-Watch "Running Codex autofix for: $ChangedFile"
    codex exec "The file '$ChangedFile' was just modified. Check if it builds correctly with 'dotnet build --warnaserror'. If there are build errors, fix them. If it builds cleanly, report success."
}

# ══════════════════════════════════════════════════════════════════
#  FileSystemWatcher 설정
# ══════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  PdfSpecDiffReporter — Folder Watch Active                  ║" -ForegroundColor Cyan  
Write-Host "║  Watching .cs files for changes                             ║" -ForegroundColor Cyan
Write-Host "║  Press Ctrl+C to stop                                       ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$watchers = @()
$lastTrigger = [datetime]::MinValue

foreach ($path in $WatchPaths) {
    if (-not (Test-Path $path)) {
        Write-Info "Path not yet created, will be watched after project setup: $path"
        continue
    }
    
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = $path
    $watcher.Filter = "*.cs"
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents = $false
    $watchers += $watcher
    Write-Watch "Watching: $path"
}

if ($watchers.Count -eq 0) {
    Write-Info "No watch paths found. Run Phase 0 first to create project structure."
    Write-Info "Then restart this script."
    exit 0
}

# ── 메인 루프 ─────────────────────────────────────────────────────
try {
    foreach ($w in $watchers) { $w.EnableRaisingEvents = $true }
    
    while ($true) {
        $changed = $false
        $changedFile = ""
        
        foreach ($w in $watchers) {
            $result = $w.WaitForChanged(
                [System.IO.WatcherChangeTypes]::Changed -bor 
                [System.IO.WatcherChangeTypes]::Created -bor 
                [System.IO.WatcherChangeTypes]::Renamed, 
                1000  # 1초 대기
            )
            if (-not $result.TimedOut) {
                $changed = $true
                $changedFile = $result.Name
                break
            }
        }
        
        if ($changed) {
            # 디바운스: 설정된 시간 내 반복 트리거 방지
            $now = Get-Date
            if (($now - $lastTrigger).TotalSeconds -lt $Debounce) {
                continue
            }
            $lastTrigger = $now
            
            Write-Host ""
            Write-Watch "Change detected: $changedFile"
            Write-Host "─────────────────────────────────────────" -ForegroundColor DarkGray
            
            # 1. 빌드
            $buildResult = Invoke-Build
            
            # 2. 테스트 (빌드 성공 시에만)
            if ($buildResult -eq 0 -and -not $BuildOnly) {
                Invoke-Tests
            }
            
            # 3. 리뷰 (선택적)
            if ($buildResult -eq 0 -and $WithReview) {
                Invoke-CodexReview
            }
            
            Write-Host "─────────────────────────────────────────" -ForegroundColor DarkGray
            Write-Watch "Waiting for next change..."
        }
    }
}
finally {
    foreach ($w in $watchers) {
        $w.EnableRaisingEvents = $false
        $w.Dispose()
    }
    Write-Info "Folder watch stopped."
}
