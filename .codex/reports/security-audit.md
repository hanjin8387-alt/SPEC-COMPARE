# Security Audit Report - 2026-02-25
## Summary: 5 PASS / 0 FAIL / 4 WARNING

| # | Check | Status | File:Line | Severity | Detail |
|---|-------|--------|-----------|----------|--------|
| 1 | HC-1 External AI API calls blocked | PASS | N/A (no matches in production .cs scan for HttpClient/WebRequest/RestClient/HttpWebRequest) | High | No direct external AI/API client usage found in runtime source set. |
| 2 | HC-2 Runtime network access blocked | PASS | N/A (no matches in production .cs scan for System.Net/Socket/TcpClient/UdpClient) | High | No explicit runtime networking primitives found. |
| 3 | HC-3 No full-text disk persistence | PASS | PdfSpecDiffReporter/Helpers/ExcelReporter.cs:13, PdfSpecDiffReporter/Helpers/ExcelReporter.cs:83, PdfSpecDiffReporter/Helpers/ExcelReporter.cs:142, PdfSpecDiffReporter/Helpers/ExcelReporter.cs:254 | High | Disk write path is report output only (.xlsx) and excerpts are capped at 500 chars. |
| 4 | HC-4 No document content logging | PASS | PdfSpecDiffReporter/Program.cs:61, PdfSpecDiffReporter/Program.cs:241, PdfSpecDiffReporter/Program.cs:254, PdfSpecDiffReporter/Program.cs:260 | High | No Console.WriteLine/Debug.WriteLine/ILogger usage found; output is status/sanitized error oriented. |
| 5 | HC-5 Exception message sanitization | PASS | PdfSpecDiffReporter/Program.cs:253, PdfSpecDiffReporter/Program.cs:259, PdfSpecDiffReporter/Pipeline/DiffEngine.cs:73, PdfSpecDiffReporter/Pipeline/SecureIngestion.cs:35, PdfSpecDiffReporter/Pipeline/TextExtractor.cs:56, PdfSpecDiffReporter/Helpers/ExcelReporter.cs:88, PdfSpecDiffReporter/Helpers/InputValidator.cs:32 | High | Catch blocks use ExceptionSanitizer.Sanitize/Wrap or fixed validation text (no raw ex.Message exposure). |
| 6 | S-1 Wrap() type-name exposure | WARNING | PdfSpecDiffReporter/Helpers/ExceptionSanitizer.cs:57 | Medium | Wrap() includes exception.GetType().Name and leaks internal exception taxonomy. |
| 7 | S-2 Program path disclosure | WARNING | PdfSpecDiffReporter/Program.cs:122, PdfSpecDiffReporter/Program.cs:241 | Low | Full config/output paths are printed to console and may expose local environment layout. |
| 8 | S-3 NuGet audit disabled | WARNING | Directory.Build.props:3, Directory.Build.props:5 | Medium | NuGetAudit=false and NU1900 exclusion reduce supply-chain visibility. |
| 9 | S-4 PdfPig custom package supply-chain risk | WARNING | PdfSpecDiffReporter/PdfSpecDiffReporter.csproj:18, NuGet.Config:8 | Medium | UglyToad.PdfPig is pinned to 1.7.0-custom-5 and requires provenance verification. |

## Risks
- Disabled NuGet vulnerability audit may miss package CVEs during restore/build.
- Custom PdfPig build introduces provenance and tampering risk without strong artifact controls.
- Exception type/path metadata leakage is low-to-medium but avoidable.

## Recommendations
1. Enable NuGet auditing in CI and fail on high/critical advisories.
2. For 1.7.0-custom-5, document source commit, build reproducibility, checksum/signing, and SBOM.
3. Update ExceptionSanitizer.Wrap() to avoid exposing runtime type names; keep correlation-only token.
4. Make path output optional behind a verbose/debug flag, defaulting to non-sensitive status text.
