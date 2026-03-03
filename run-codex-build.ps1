<#
    PdfSpecDiffReporter Codex orchestration script.
    - Runs phase-based Codex prompts
    - Applies build/test/publish gates
    - Auto-fixes missing Windows process environment vars for dotnet/NuGet
#>

param(
    [int]$Phase = 0,
    [switch]$SkipGate,
    [switch]$Interactive,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$EnvBootstrapPath = Join-Path $ProjectDir "scripts/EnvironmentDefaults.ps1"

if (-not (Test-Path $EnvBootstrapPath)) {
    throw "Missing environment bootstrap script: $EnvBootstrapPath"
}

. $EnvBootstrapPath

function Write-Phase([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Step([string]$Message)  { Write-Host "  - $Message" -ForegroundColor Green }
function Write-Gate([string]$Message)  { Write-Host "  [GATE] $Message" -ForegroundColor Yellow }
function Write-Err([string]$Message)   { Write-Host "  [ERR] $Message" -ForegroundColor Red }

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "  > dotnet $($Arguments -join ' ')" -ForegroundColor DarkCyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed: dotnet $($Arguments -join ' ')"
    }
}

function Test-BuildGate {
    if ($SkipGate) {
        Write-Gate "Build gate skipped (--SkipGate)"
        return
    }
    Write-Gate "Running dotnet build --warnaserror"
    Push-Location $ProjectDir
    try {
        Invoke-Dotnet -Arguments @("build", "--warnaserror")
        Write-Gate "Build gate passed"
    }
    finally { Pop-Location }
}

function Test-TestGate {
    if ($SkipGate) {
        Write-Gate "Test gate skipped (--SkipGate)"
        return
    }
    Write-Gate "Running dotnet test --configuration Release --verbosity normal"
    Push-Location $ProjectDir
    try {
        Invoke-Dotnet -Arguments @("test", "--configuration", "Release", "--verbosity", "normal")
        Write-Gate "Test gate passed"
    }
    finally { Pop-Location }
}

function Test-PublishGate {
    if ($SkipGate) {
        Write-Gate "Publish gate skipped (--SkipGate)"
        return
    }
    Write-Gate "Running dotnet publish (win-x64 single-file)"
    Push-Location $ProjectDir
    try {
        Invoke-Dotnet -Arguments @(
            "publish",
            "PdfSpecDiffReporter/PdfSpecDiffReporter.csproj",
            "-c", "Release",
            "-r", "win-x64",
            "--self-contained", "false",
            "/p:PublishSingleFile=true"
        )
        Write-Gate "Publish gate passed"
    }
    finally { Pop-Location }
}

function Invoke-Codex {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Write-Step $Label
    if ($DryRun) {
        Write-Host "    [DRY-RUN] codex exec <prompt>" -ForegroundColor DarkGray
        return
    }

    & codex exec --skip-git-repo-check $Prompt
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Codex exec failed: $Label"
        throw "Codex exec failed: $Label"
    }
}

function Invoke-CodexMultiAgent {
    param(
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Write-Step "$Label (multi-agent)"
    if ($DryRun) {
        Write-Host "    [DRY-RUN] codex exec <multi-agent prompt>" -ForegroundColor DarkGray
        return
    }

    & codex exec --skip-git-repo-check $Prompt
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Codex multi-agent failed: $Label"
        throw "Codex multi-agent failed: $Label"
    }
}

Initialize-WindowsEnvironmentDefaults

if ($Phase -le 0) {
    Write-Phase "PHASE 0: Project Setup"
    Invoke-Codex -Label "Project scaffolding" -Prompt @"
Read codex/SKILL.md and codex/agents/01_project_setup/SKILL.md carefully.
Then execute Phase 0 from codex/workflow.md:
1. Create solution and projects
2. Add projects to solution
3. Add approved NuGet packages
4. Configure single-file publish
5. Create Models/, Pipeline/, Helpers/
6. Create Program.cs CLI skeleton
7. Verify dotnet build --warnaserror
"@
    Test-BuildGate
    Write-Phase "PHASE 0 COMPLETE"
}

if ($Phase -le 1) {
    Write-Phase "PHASE 1: PDF Extraction + Text Cleanup"
    Invoke-CodexMultiAgent -Label "PDF extraction + text cleanup" -Prompt @"
This is a multi-agent task. Spawn TWO parallel agents:

AGENT 1 (builder role): Read codex/agents/02_pdf_extract/SKILL.md.
Implement SecureIngestion.cs and TextExtractor.cs in Pipeline/.

AGENT 2 (builder role): Read codex/agents/03_text_cleanup/SKILL.md.
Implement TextNormalizer.cs in Pipeline/.

Wait for both agents to complete, then verify dotnet build --warnaserror.
"@
    Test-BuildGate
    Write-Phase "PHASE 1 COMPLETE"
}

if ($Phase -le 2) {
    Write-Phase "PHASE 2: Chapter Segmentation + Matching"
    Invoke-Codex -Label "Chapter split and match" -Prompt @"
Read codex/agents/04_chapter_split_match/SKILL.md carefully.
Execute Phase 2 from codex/workflow.md:
1. Implement ChapterNode model
2. Implement ChapterSegmenter.cs
3. Implement ChapterMatcher.cs
4. Verify dotnet build --warnaserror
"@
    Test-BuildGate
    Write-Phase "PHASE 2 COMPLETE"
}

if ($Phase -le 3) {
    Write-Phase "PHASE 3: Diff Engine + Excel Writer"
    Invoke-CodexMultiAgent -Label "Diff engine + Excel report" -Prompt @"
This is a multi-agent task. Spawn TWO parallel agents:

AGENT 1 (builder role): Read codex/agents/05_diff_engine/SKILL.md.
Implement SimilarityCalculator.cs and DiffEngine.cs.

AGENT 2 (builder role): Read codex/agents/06_excel_writer/SKILL.md.
Implement ExcelReporter.cs.

Wait for both agents, then verify dotnet build --warnaserror.
"@
    Test-BuildGate
    Write-Phase "PHASE 3 COMPLETE"
}

if ($Phase -le 4) {
    Write-Phase "PHASE 4: CLI UX + Tests"
    Invoke-CodexMultiAgent -Label "CLI UX + tests + security review" -Prompt @"
This is a multi-agent task. Spawn THREE parallel agents:

AGENT 1 (builder role): Read codex/agents/07_cli_ux_tests/SKILL.md.
Complete Program.cs CLI UX and error handling.

AGENT 2 (tester role): Write/extend unit tests for all components.

AGENT 3 (reviewer role): Review HC-1..HC-5 security constraints.

Wait for all agents, then summarize results.
"@
    Test-BuildGate
    Test-TestGate
    Write-Phase "PHASE 4 COMPLETE"
}

if ($Phase -le 5) {
    Write-Phase "PHASE 5: Final Publish + Acceptance"
    Invoke-Codex -Label "Final acceptance checks" -Prompt @"
Final acceptance phase:
1. Run full test suite
2. Run security constraint scan (HC-1..HC-5)
3. Publish single-file EXE
4. Verify publish output path and artifact sizes
5. Report final status
"@
    Test-PublishGate
    Write-Phase "BUILD COMPLETE - ALL PHASES DONE"
}
