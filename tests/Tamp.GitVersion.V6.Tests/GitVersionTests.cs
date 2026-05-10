using Xunit;

namespace Tamp.GitVersion.V6.Tests;

public sealed class GitVersionTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/dotnet-gitversion"));

    private static int IndexOf(IReadOnlyList<string> args, string value, int start = 0)
    {
        for (var i = start; i < args.Count; i++)
            if (args[i] == value) return i;
        return -1;
    }

    [Fact]
    public void Run_Throws_On_Null_Tool()
        => Assert.Throws<ArgumentNullException>(() => GitVersion.Run(null!));

    [Fact]
    public void Run_Bare_Has_Empty_Args()
    {
        var plan = GitVersion.Run(FakeTool());
        Assert.Empty(plan.Arguments);
        Assert.Empty(plan.Secrets);
    }

    [Fact]
    public void Run_Executable_Is_The_Tool_Path()
    {
        var plan = GitVersion.Run(FakeTool());
        Assert.Equal("/fake/dotnet-gitversion", plan.Executable);
    }

    [Fact]
    public void TargetPath_Maps_To_Slash_TargetPath()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetTargetPath("/repo")).Arguments;
        Assert.Equal("/targetpath", args[0]);
        Assert.Equal("/repo", args[1]);
    }

    [Theory]
    [InlineData(GitVersionOutput.Json, "json")]
    [InlineData(GitVersionOutput.File, "file")]
    [InlineData(GitVersionOutput.BuildServer, "buildserver")]
    [InlineData(GitVersionOutput.DotEnv, "dotenv")]
    public void Output_Maps_To_Lowercase_Token(GitVersionOutput output, string expected)
    {
        var args = GitVersion.Run(FakeTool(), s => s.AddOutput(output)).Arguments;
        Assert.Equal(expected, args[IndexOf(args, "/output") + 1]);
    }

    [Fact]
    public void Output_Multiple_Each_Get_Their_Own_Slash_Output()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .AddOutput(GitVersionOutput.Json)
            .AddOutput(GitVersionOutput.BuildServer)
            .AddOutput(GitVersionOutput.DotEnv)).Arguments;
        Assert.Equal(3, args.Count(a => a == "/output"));
    }

    [Fact]
    public void OutputFile_Round_Trips()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetOutputFile("/tmp/v.json")).Arguments;
        Assert.Equal("/tmp/v.json", args[IndexOf(args, "/outputfile") + 1]);
    }

    [Fact]
    public void ShowVariable_Round_Trips()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetShowVariable("SemVer")).Arguments;
        Assert.Equal("SemVer", args[IndexOf(args, "/showvariable") + 1]);
    }

    [Fact]
    public void Format_String_Round_Trips()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetFormat("{Major}.{Minor}.{Patch}")).Arguments;
        Assert.Equal("{Major}.{Minor}.{Patch}", args[IndexOf(args, "/format") + 1]);
    }

    [Fact]
    public void LogFile_Maps_To_Slash_L()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetLogFile("console")).Arguments;
        Assert.Equal("console", args[IndexOf(args, "/l") + 1]);
    }

    [Fact]
    public void ConfigFile_Round_Trips()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetConfigFile("./gitversion.yml")).Arguments;
        Assert.Equal("./gitversion.yml", args[IndexOf(args, "/config") + 1]);
    }

    [Fact]
    public void ShowConfig_Emits_Flag()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetShowConfig()).Arguments;
        Assert.Contains("/showconfig", args);
    }

    [Fact]
    public void OverrideConfig_Each_Pair_Gets_Its_Own_Slash_OverrideConfig()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetOverrideConfig("tag-prefix", "v")
            .SetOverrideConfig("update-build-number", "false")).Arguments;
        Assert.Equal(2, args.Count(a => a == "/overrideconfig"));
        Assert.Contains("tag-prefix=v", args);
        Assert.Contains("update-build-number=false", args);
    }

    [Fact]
    public void NoCache_NoNormalize_AllowShallow_All_Round_Trip()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetNoCache().SetNoNormalize().SetAllowShallow()).Arguments;
        Assert.Contains("/nocache", args);
        Assert.Contains("/nonormalize", args);
        Assert.Contains("/allowshallow", args);
    }

    [Theory]
    [InlineData(GitVersionVerbosity.Quiet, "Quiet")]
    [InlineData(GitVersionVerbosity.Minimal, "Minimal")]
    [InlineData(GitVersionVerbosity.Normal, "Normal")]
    [InlineData(GitVersionVerbosity.Verbose, "Verbose")]
    [InlineData(GitVersionVerbosity.Diagnostic, "Diagnostic")]
    public void Verbosity_Maps_To_Title_Case_Token(GitVersionVerbosity v, string expected)
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetVerbosity(v)).Arguments;
        Assert.Equal(expected, args[IndexOf(args, "/verbosity") + 1]);
    }

    [Fact]
    public void Diagnostic_Emits_Slash_Diag()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetDiagnostic()).Arguments;
        Assert.Contains("/diag", args);
    }

    [Fact]
    public void UpdateAssemblyInfo_With_No_Files_Emits_Just_The_Flag()
    {
        var args = GitVersion.Run(FakeTool(), s => s.SetUpdateAssemblyInfo()).Arguments;
        Assert.Contains("/updateassemblyinfo", args);
    }

    [Fact]
    public void UpdateAssemblyInfo_With_Files_Emits_Files_After_Flag()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetUpdateAssemblyInfo()
            .AddAssemblyInfoFile("src/A/AssemblyInfo.cs")
            .AddAssemblyInfoFile("src/B/AssemblyInfo.cs")).Arguments;
        var i = IndexOf(args, "/updateassemblyinfo");
        Assert.Equal("src/A/AssemblyInfo.cs", args[i + 1]);
        Assert.Equal("src/B/AssemblyInfo.cs", args[i + 2]);
    }

    [Fact]
    public void UpdateProjectFiles_EnsureAssemblyInfo_UpdateWixVersionFile_All_Round_Trip()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetUpdateProjectFiles().SetEnsureAssemblyInfo().SetUpdateWixVersionFile()).Arguments;
        Assert.Contains("/updateprojectfiles", args);
        Assert.Contains("/ensureassemblyinfo", args);
        Assert.Contains("/updatewixversionfile", args);
    }

    [Fact]
    public void Remote_Url_Branch_User_Commit_Round_Trip()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetUrl("https://github.com/x/y.git")
            .SetBranch("main")
            .SetUsername("scott")
            .SetCommit("abc123")).Arguments;
        Assert.Equal("https://github.com/x/y.git", args[IndexOf(args, "/url") + 1]);
        Assert.Equal("main", args[IndexOf(args, "/b") + 1]);
        Assert.Equal("scott", args[IndexOf(args, "/u") + 1]);
        Assert.Equal("abc123", args[IndexOf(args, "/c") + 1]);
    }

    [Fact]
    public void Remote_Password_Emits_Revealed_Value_And_Registers_Secret()
    {
        var pw = new Secret("GitToken", "ghp_abc123");
        var plan = GitVersion.Run(FakeTool(), s => s.SetPassword(pw));
        Assert.Equal("ghp_abc123", plan.Arguments[IndexOf(plan.Arguments, "/p") + 1]);
        Assert.Same(pw, Assert.Single(plan.Secrets));
    }

    [Fact]
    public void NoFetch_And_DynamicRepoLocation_Round_Trip()
    {
        var args = GitVersion.Run(FakeTool(), s => s
            .SetNoFetch()
            .SetDynamicRepoLocation("/tmp/dynamic")).Arguments;
        Assert.Contains("/nofetch", args);
        Assert.Equal("/tmp/dynamic", args[IndexOf(args, "/dynamicRepoLocation") + 1]);
    }

    [Fact]
    public void EnvironmentVariables_Pass_Through_To_Plan()
    {
        var plan = GitVersion.Run(FakeTool(), s => s.SetEnvironmentVariable("FOO", "BAR"));
        Assert.Equal("BAR", plan.Environment["FOO"]);
    }

    [Fact]
    public void WorkingDirectory_From_Settings_Wins_Over_Tool()
    {
        var tool = new Tool(AbsolutePath.Create("/fake/gv"), workingDirectory: "/from-tool");
        var plan = GitVersion.Run(tool, s => s.SetWorkingDirectory("/from-settings"));
        Assert.Equal("/from-settings", plan.WorkingDirectory);
    }

    [Fact]
    public void Common_Build_Script_Shape_SemVer_Only_Round_Trips_All_Args_In_Order()
    {
        // Realistic single-variable invocation: targetpath + showvariable.
        var args = GitVersion.Run(FakeTool(), s => s
            .SetTargetPath("/repo")
            .SetShowVariable("SemVer")
            .SetOverrideConfig("tag-prefix", "v")).Arguments;
        Assert.Equal(["/targetpath", "/repo", "/showvariable", "SemVer", "/overrideconfig", "tag-prefix=v"], args);
    }
}
