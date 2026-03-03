---
name: project-setup
description: >
  Phase 0 agent — scaffolds the .NET 8 solution, adds NuGet dependencies,
  configures single-file publish, and creates the directory layout for
  PdfSpecDiffReporter.
---

# Agent 01 — Project Setup

## Scope

Create the repository skeleton so that subsequent agents have a compilable,
publishable .NET 8 project to build upon.

## Inputs

- `codex/SKILL.md` (read first for constraints & approved NuGet list)
- `codex/workflow.md` Phase 0 section

## Outputs

- `PdfSpecDiffReporter.sln`
- `PdfSpecDiffReporter/PdfSpecDiffReporter.csproj` (configured)
- `PdfSpecDiffReporter/Program.cs` (CLI skeleton)
- `PdfSpecDiffReporter/Pipeline/` directory (empty, ready)
- `PdfSpecDiffReporter/Models/` directory
- `PdfSpecDiffReporter/Helpers/` directory
- `PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj`
- All NuGet packages restored

## Acceptance Criteria

1. `dotnet build --configuration Release --warnaserror` → **0 errors, 0 warnings**
2. `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true` → produces a single EXE
3. Running the EXE with `--help` prints usage information
4. `dotnet test` → all tests pass (even if only a placeholder test exists)
5. `.csproj` contains `<PublishSingleFile>true</PublishSingleFile>` and `<SelfContained>true</SelfContained>`

## Detailed Instructions

### Step 1: Create Solution

```bash
dotnet new sln -n PdfSpecDiffReporter
dotnet new console -n PdfSpecDiffReporter -f net8.0
dotnet new xunit -n PdfSpecDiffReporter.Tests -f net8.0
dotnet sln add PdfSpecDiffReporter/PdfSpecDiffReporter.csproj
dotnet sln add PdfSpecDiffReporter.Tests/PdfSpecDiffReporter.Tests.csproj
dotnet add PdfSpecDiffReporter.Tests reference PdfSpecDiffReporter
```

### Step 2: Add NuGet Packages

```bash
cd PdfSpecDiffReporter
dotnet add package UglyToad.PdfPig
dotnet add package ClosedXML
dotnet add package DiffPlex
dotnet add package System.CommandLine --prerelease
dotnet add package Spectre.Console
cd ../PdfSpecDiffReporter.Tests
dotnet add package Moq
dotnet add package FluentAssertions
```

### Step 3: Configure .csproj

Add to `PdfSpecDiffReporter.csproj` `<PropertyGroup>`:

```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

### Step 4: Create Directories

```
PdfSpecDiffReporter/
├── Pipeline/
├── Models/
└── Helpers/
```

### Step 5: Implement CLI Skeleton (`Program.cs`)

Use `System.CommandLine` to define:

- `source_pdf` argument (required, string path)
- `target_pdf` argument (required, string path)
- `--output` / `-o` option (default: `.\diff_report.xlsx`)
- `--config` / `-c` option (optional JSON config path)
- `--threshold` option (default: 0.85, double)

The handler should print a placeholder message and exit with code 0.

### Step 6: Create Model Stubs

Create `Models/ChapterNode.cs`, `Models/DiffItem.cs`, `Models/ChangeType.cs`
with the data contracts from `codex/SKILL.md` §7.

### Step 7: Create ExceptionSanitizer

Create `Helpers/ExceptionSanitizer.cs` with the pattern from `codex/SKILL.md` §3.

## Edge Cases

- Ensure `.csproj` does NOT set `RuntimeIdentifier` if it conflicts with `dotnet test` on non-win-x64 dev machines. Use conditional `<RuntimeIdentifier>` or only set it in publish profile.
- `System.CommandLine` is still prerelease — pin to a specific version to avoid breaking changes.

## Never Do

- ❌ Add packages not on the approved list without justification
- ❌ Use `Console.WriteLine` to log document content
- ❌ Create any temp files on disk
- ❌ Add network-calling packages (HttpClient, RestSharp, etc.)

## Suggested Unit Tests

```csharp
[Fact]
public void Program_HelpFlag_ReturnsZeroExitCode()
{
    // Invoke the CLI with --help and verify exit code 0
}

[Fact]
public void ExceptionSanitizer_NeverExposesOriginalMessage()
{
    var ex = new InvalidOperationException("secret PDF content here");
    var sanitized = ExceptionSanitizer.Sanitize(ex);
    Assert.DoesNotContain("secret", sanitized);
    Assert.DoesNotContain("PDF", sanitized);
}
```
