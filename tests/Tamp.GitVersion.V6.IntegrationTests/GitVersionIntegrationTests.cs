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
        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var p = Path.Combine(home, ".dotnet", "tools", "dotnet-gitversion");
        if (!File.Exists(p))
            throw new InvalidOperationException(
                $"dotnet-gitversion not found at {p}. Install with: dotnet tool install -g GitVersion.Tool --version 6.*");
        return new Tool(AbsolutePath.Create(p));
    }

    private static string TampRepoRoot()
    {
        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var path = Path.Combine(home, "repos", "tamp");
        if (!Directory.Exists(Path.Combine(path, ".git")))
            throw new InvalidOperationException($"Sibling tamp checkout not at {path}.");
        return path;
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
