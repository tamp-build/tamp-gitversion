using Xunit;

namespace Tamp.GitVersion.V6.Tests;

// ---- Object-init overloads (TAM-161, 0.1.1+) ----
public sealed class ObjectInitTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/dotnet-gitversion"));

    [Fact]
    public void Run_ObjectInit_Emits_Identical_Plan_To_Fluent()
    {
        var pw = new Secret("GitToken", "ghp_abc123");

        var fluent = GitVersion.Run(FakeTool(), s => s
            .SetTargetPath("/repo")
            .SetShowVariable("SemVer")
            .AddOutput(GitVersionOutput.Json)
            .AddOutput(GitVersionOutput.BuildServer)
            .SetOverrideConfig("tag-prefix", "v")
            .SetNoCache()
            .SetVerbosity(GitVersionVerbosity.Diagnostic)
            .SetUpdateAssemblyInfo()
            .AddAssemblyInfoFile("src/A/AssemblyInfo.cs")
            .SetUrl("https://github.com/x/y.git")
            .SetBranch("main")
            .SetUsername("scott")
            .SetPassword(pw)
            .SetCommit("abc123")
            .SetNoFetch()
            .SetEnvironmentVariable("FOO", "BAR"));

        var objectInit = GitVersion.Run(FakeTool(), new GitVersionSettings
        {
            TargetPath = "/repo",
            ShowVariable = "SemVer",
            Outputs = { GitVersionOutput.Json, GitVersionOutput.BuildServer },
            OverrideConfig = { ["tag-prefix"] = "v" },
            NoCache = true,
            Verbosity = GitVersionVerbosity.Diagnostic,
            UpdateAssemblyInfo = true,
            AssemblyInfoFiles = { "src/A/AssemblyInfo.cs" },
            Url = "https://github.com/x/y.git",
            Branch = "main",
            Username = "scott",
            Password = pw,
            Commit = "abc123",
            NoFetch = true,
            EnvironmentVariables = { ["FOO"] = "BAR" },
        });

        Assert.Equal(fluent.Executable, objectInit.Executable);
        Assert.Equal(fluent.Arguments, objectInit.Arguments);
        Assert.Equal(fluent.Environment, objectInit.Environment);
    }

    [Fact]
    public void Run_ObjectInit_Surface_Returns_NonNull_CommandPlan()
    {
        // Smoke test: the wrapper accepts an object-init settings argument and returns a non-null CommandPlan.
        Assert.NotNull(GitVersion.Run(FakeTool(), new GitVersionSettings { TargetPath = "/repo" }));
    }

    [Fact]
    public void Run_ObjectInit_Throws_On_Null_Tool()
        => Assert.Throws<ArgumentNullException>(() => GitVersion.Run(null!, new GitVersionSettings()));

    [Fact]
    public void Run_ObjectInit_Throws_On_Null_Settings()
        => Assert.Throws<ArgumentNullException>(() => GitVersion.Run(FakeTool(), (GitVersionSettings)null!));
}
