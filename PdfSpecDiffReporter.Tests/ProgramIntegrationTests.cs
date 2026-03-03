using System.Diagnostics;
using PdfSpecDiffReporter.Helpers;

namespace PdfSpecDiffReporter.Tests;

public sealed class ProgramIntegrationTests
{
    [Fact]
    public void Main_WithHelpFlag_ReturnsZero()
    {
        var result = RunApplication("--help");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Main_WithMissingArgs_ReturnsNonZero()
    {
        var result = RunApplication();

        Assert.NotEqual(0, result.ExitCode);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunApplication(params string[] args)
    {
        var appAssemblyPath = typeof(InputValidator).Assembly.Location;
        var appHostPath = Path.ChangeExtension(appAssemblyPath, ".exe");
        var runWithDotnet = !File.Exists(appHostPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = runWithDotnet ? "dotnet" : appHostPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (runWithDotnet)
        {
            startInfo.ArgumentList.Add(appAssemblyPath);
        }

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        if (!process!.WaitForExit(15000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI process did not exit within timeout.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        return (process.ExitCode, stdout, stderr);
    }
}
