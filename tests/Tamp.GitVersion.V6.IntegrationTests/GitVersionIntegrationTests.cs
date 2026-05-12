using System.IO;
using System.Text.Json;
using Tamp;
using Xunit;
using Xunit.Abstractions;

namespace Tamp.GitVersion.V6.IntegrationTests;

/// <summary>
/// Real-tool exercises of the wrapper against an actual git repo.
/// Uses the tamp main checkout (sibling clone at ~/repos/tamp) — that's
/// a real git history with tags + a real branch, so GitVersion has
/// something to chew on.
/// </summary>
public sealed class GitVersionIntegrationTests
{
    private readonly ITestOutputHelper _output;
    public GitVersionIntegrationTests(ITestOutputHelper output) => _output = output;

    private static Tool ResolveTool()
    {
        // Walk PATH first (covers `dotnet tool install -g` adding ~/.dotnet/tools to PATH on every OS).
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var executable = OperatingSystem.IsWindows() ? "dotnet-gitversion.exe" : "dotnet-gitversion";
        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, executable);
            if (File.Exists(candidate)) return new Tool(AbsolutePath.Create(candidate));
        }
        // Fall back to ~/.dotnet/tools (HOME on POSIX, USERPROFILE on Windows).
        var home = Environment.GetEnvironmentVariable("HOME")
            ?? Environment.GetEnvironmentVariable("USERPROFILE")
            ?? "";
        if (!string.IsNullOrEmpty(home))
        {
            var p = Path.Combine(home, ".dotnet", "tools", executable);
            if (File.Exists(p)) return new Tool(AbsolutePath.Create(p));
        }
        throw new InvalidOperationException(
            "dotnet-gitversion not found on PATH or in ~/.dotnet/tools. Install with: dotnet tool install -g GitVersion.Tool --version 6.*");
    }

    /// <summary>
    /// Locates a git checkout to run GitVersion against. CI uses the runner's
    /// own checkout root via <c>GITHUB_WORKSPACE</c>; local invocations walk
    /// up from the test binary looking for a <c>.git</c> directory. The test
    /// suite doesn't require the TAMP repo specifically — any git checkout
    /// with a SemVer-tagged history works for the assertions.
    /// </summary>
    private static string TampRepoRoot()
    {
        var ws = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrEmpty(ws) && Directory.Exists(Path.Combine(ws, ".git")))
            return ws;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir || parent is null) break;
            dir = parent;
        }
        throw new InvalidOperationException(
            $"No git checkout found above '{AppContext.BaseDirectory}' or at $GITHUB_WORKSPACE.");
    }

    private CaptureResult Run(CommandPlan plan)
    {
        _output.WriteLine($"$ {plan.Executable} {string.Join(' ', plan.Arguments)}");
        var result = ProcessRunner.Capture(plan);
        foreach (var line in result.Lines)
            _output.WriteLine($"  [{line.Type}] {line.Text}");
        _output.WriteLine($"  → exit {result.ExitCode}");
        return result;
    }

    [Fact]
    public void Run_Bare_Against_Tamp_Repo_Emits_JSON_With_SemVer_Field()
    {
        var tool = ResolveTool();
        var plan = GitVersion.Run(tool, s => s.SetTargetPath(TampRepoRoot()));
        var result = Run(plan);
        Assert.Equal(0, result.ExitCode);

        // The default output is JSON to stdout. Parse it and confirm
        // SemVer is present.
        var json = result.StdoutText;
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("SemVer", out var semVer));
        Assert.False(string.IsNullOrEmpty(semVer.GetString()));
    }

    [Fact]
    public void ShowVariable_SemVer_Emits_Just_The_Version_String()
    {
        var tool = ResolveTool();
        var plan = GitVersion.Run(tool, s => s
            .SetTargetPath(TampRepoRoot())
            .SetShowVariable("SemVer"));
        var result = Run(plan);
        Assert.Equal(0, result.ExitCode);

        // /showvariable SemVer prints just the value, no JSON envelope.
        var stdout = result.StdoutText.Trim();
        // Loose check — SemVer ought to start with a digit.
        Assert.NotEmpty(stdout);
        Assert.True(char.IsDigit(stdout[0]),
            $"Expected SemVer to start with a digit but got: {stdout}");
    }

    [Fact]
    public void Format_Produces_Custom_String_From_Variables()
    {
        var tool = ResolveTool();
        var plan = GitVersion.Run(tool, s => s
            .SetTargetPath(TampRepoRoot())
            .SetFormat("{Major}.{Minor}"));
        var result = Run(plan);
        Assert.Equal(0, result.ExitCode);

        var stdout = result.StdoutText.Trim();
        // Expect: <major>.<minor> — two integers separated by a dot.
        var parts = stdout.Split('.');
        Assert.True(parts.Length >= 2,
            $"Expected at least Major.Minor format but got: {stdout}");
        Assert.True(int.TryParse(parts[0], out _));
        Assert.True(int.TryParse(parts[1], out _));
    }

    [Fact]
    public void ShowConfig_Outputs_Yaml_With_Real_Top_Level_Keys()
    {
        var tool = ResolveTool();
        var plan = GitVersion.Run(tool, s => s
            .SetTargetPath(TampRepoRoot())
            .SetShowConfig());
        var result = Run(plan);
        Assert.Equal(0, result.ExitCode);

        // GitVersion 6's effective config starts with these top-level keys.
        var stdout = result.StdoutText;
        Assert.Contains("mode:", stdout, StringComparison.Ordinal);
        Assert.Contains("tag-prefix:", stdout, StringComparison.Ordinal);
        Assert.Contains("increment:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_BuildServer_Runs_In_BuildServer_Mode_End_To_End()
    {
        var tool = ResolveTool();
        var plan = GitVersion.Run(tool, s => s
            .SetTargetPath(TampRepoRoot())
            .AddOutput(GitVersionOutput.BuildServer));
        var result = Run(plan);
        Assert.Equal(0, result.ExitCode);

        // BuildServer mode emits CI-specific markers (##teamcity[…],
        // ##vso[…]) only when running under that CI. Locally GitVersion
        // detects "LocalBuild" and skips emission. Either branch is
        // valid — what we're confirming here is that the wrapper's
        // /output buildserver flag was accepted (exit 0) and the run
        // completed in BuildServer mode (the LocalBuild banner appears
        // only in this output path).
        var combined = result.StdoutText + "\n" + result.StderrText;
        Assert.True(
            combined.Contains("LocalBuild", StringComparison.Ordinal) ||
            combined.Contains("##teamcity", StringComparison.Ordinal) ||
            combined.Contains("##vso", StringComparison.Ordinal) ||
            combined.Contains("GitVersion_SemVer", StringComparison.Ordinal),
            "Expected one of LocalBuild / ##teamcity / ##vso / GitVersion_SemVer in output.");
    }
}
