using System.Text.Json;
using Xunit;

namespace Tamp.GitVersion.V6.Tests;

/// <summary>
/// Tests for the <see cref="GitVersionAttribute"/> + <see cref="GitVersionInfo"/>
/// value-injection surface (HoldFast canary friction #17, TAM-118). Verifies
/// the JSON-to-typed-record parsing path + executable-resolution heuristic.
/// Actual subprocess invocation is covered by the integration test project
/// (only runs when GitVersion.Tool is installed).
/// </summary>
public sealed class GitVersionAttributeTests
{
    private const string CanonicalGitVersionOutput = """
    {
      "Major": 1,
      "Minor": 2,
      "Patch": 3,
      "PreReleaseTag": "alpha.7",
      "PreReleaseTagWithDash": "-alpha.7",
      "PreReleaseLabel": "alpha",
      "PreReleaseNumber": 7,
      "WeightedPreReleaseNumber": 1007,
      "BuildMetaData": "12",
      "BuildMetaDataPadded": "0012",
      "FullBuildMetaData": "12.Branch.feature/foo.Sha.deadbeef",
      "MajorMinorPatch": "1.2.3",
      "SemVer": "1.2.3-alpha.7",
      "AssemblySemVer": "1.2.3.0",
      "AssemblySemFileVer": "1.2.3.0",
      "InformationalVersion": "1.2.3-alpha.7+12.Branch.feature-foo.Sha.deadbeef",
      "FullSemVer": "1.2.3-alpha.7+12",
      "BranchName": "feature/foo",
      "EscapedBranchName": "feature-foo",
      "Sha": "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef",
      "ShortSha": "deadbeef",
      "CommitsSinceVersionSource": 12,
      "UncommittedChanges": 0,
      "VersionSourceSha": "cafebabecafebabecafebabecafebabecafebabe",
      "CommitDate": "2026-04-12"
    }
    """;

    // ─── ParseGitVersionJson — happy path ─────────────────────────────────

    [Fact]
    public void ParseGitVersionJson_Populates_Core_Version_Fields()
    {
        var info = GitVersionAttribute.ParseGitVersionJson(CanonicalGitVersionOutput, "gitversion");
        Assert.Equal(1, info.Major);
        Assert.Equal(2, info.Minor);
        Assert.Equal(3, info.Patch);
        Assert.Equal("1.2.3", info.MajorMinorPatch);
        Assert.Equal("1.2.3-alpha.7", info.SemVer);
        Assert.Equal("1.2.3-alpha.7+12", info.FullSemVer);
    }

    [Fact]
    public void ParseGitVersionJson_Populates_Branch_And_Sha_Fields()
    {
        var info = GitVersionAttribute.ParseGitVersionJson(CanonicalGitVersionOutput, "gitversion");
        Assert.Equal("feature/foo", info.BranchName);
        Assert.Equal("feature-foo", info.EscapedBranchName);
        Assert.Equal("deadbeefdeadbeefdeadbeefdeadbeefdeadbeef", info.Sha);
        Assert.Equal("deadbeef", info.ShortSha);
    }

    [Fact]
    public void ParseGitVersionJson_Populates_PreRelease_Fields()
    {
        var info = GitVersionAttribute.ParseGitVersionJson(CanonicalGitVersionOutput, "gitversion");
        Assert.Equal("alpha.7", info.PreReleaseTag);
        Assert.Equal("-alpha.7", info.PreReleaseTagWithDash);
        Assert.Equal("alpha", info.PreReleaseLabel);
        Assert.Equal(7, info.PreReleaseNumber);
    }

    [Fact]
    public void ParseGitVersionJson_Populates_Commit_Metadata()
    {
        var info = GitVersionAttribute.ParseGitVersionJson(CanonicalGitVersionOutput, "gitversion");
        Assert.Equal(12, info.CommitsSinceVersionSource);
        Assert.Equal(0, info.UncommittedChanges);
        Assert.Equal("2026-04-12", info.CommitDate);
    }

    [Fact]
    public void ParseGitVersionJson_ToString_Returns_SemVer()
    {
        var info = GitVersionAttribute.ParseGitVersionJson(CanonicalGitVersionOutput, "gitversion");
        Assert.Equal("1.2.3-alpha.7", info.ToString());
    }

    // ─── ParseGitVersionJson — tolerant of preamble text ─────────────────

    [Fact]
    public void ParseGitVersionJson_Tolerates_Informational_Lines_Before_JSON()
    {
        // The CLI sometimes emits info to stdout before the JSON object.
        // Real-world example: "Checking out the repository..." etc.
        var output = "Some informational line\nAnother line\n" + CanonicalGitVersionOutput;
        var info = GitVersionAttribute.ParseGitVersionJson(output, "gitversion");
        Assert.Equal("1.2.3-alpha.7", info.SemVer);
    }

    // ─── ParseGitVersionJson — error paths ───────────────────────────────

    [Fact]
    public void ParseGitVersionJson_Throws_On_Output_With_No_JSON()
    {
        var output = "no curly brace here, just a CLI error message";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitVersionAttribute.ParseGitVersionJson(output, "gitversion"));
        Assert.Contains("no JSON output", ex.Message);
        Assert.Contains("gitversion", ex.Message);
    }

    [Fact]
    public void ParseGitVersionJson_Throws_On_Malformed_JSON()
    {
        // First '{' is found but the object isn't valid JSON.
        var output = "{ \"Major\": 1, this isn't valid json }";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitVersionAttribute.ParseGitVersionJson(output, "gitversion"));
        Assert.Contains("failed to parse JSON", ex.Message);
    }

    // ─── Raw extension data — newer fields survive without typed properties ─

    [Fact]
    public void ParseGitVersionJson_Captures_Unknown_Fields_In_Raw()
    {
        // GitVersion 6.x might add new fields in patch versions. Those land
        // in the Raw extension dictionary rather than getting dropped.
        var output = """
        {
          "Major": 1, "Minor": 0, "Patch": 0,
          "MajorMinorPatch": "1.0.0",
          "SemVer": "1.0.0", "FullSemVer": "1.0.0",
          "AssemblySemVer": "1.0.0.0", "AssemblySemFileVer": "1.0.0.0",
          "InformationalVersion": "1.0.0",
          "BranchName": "main", "Sha": "abc", "ShortSha": "abc",
          "FutureGitVersionField": "value-from-the-future"
        }
        """;
        var info = GitVersionAttribute.ParseGitVersionJson(output, "gitversion");
        Assert.True(info.Raw.ContainsKey("FutureGitVersionField"));
        Assert.Equal("value-from-the-future", info.Raw["FutureGitVersionField"].GetString());
    }

    // ─── Attribute defaults ─────────────────────────────────────────────

    [Fact]
    public void Attribute_Default_TimeoutSeconds_Is_30()
    {
        var attr = new GitVersionAttribute();
        Assert.Equal(30, attr.TimeoutSeconds);
    }

    [Fact]
    public void Attribute_NoFetch_Defaults_True_To_Avoid_Network_Hits()
    {
        var attr = new GitVersionAttribute();
        Assert.True(attr.NoFetch);
    }

    [Fact]
    public void Attribute_Executable_Defaults_Null_For_Autoprobe()
    {
        var attr = new GitVersionAttribute();
        Assert.Null(attr.Executable);
    }

    // ─── PATH lookup heuristic ───────────────────────────────────────────

    [Fact]
    public void IsOnPath_Returns_False_For_Definitely_Missing_Command()
    {
        // No sane developer has a binary named this on PATH.
        Assert.False(GitVersionAttribute.IsOnPath("tamp-impossible-fake-binary-name-zxcvbnm"));
    }

    [Fact]
    public void IsOnPath_Returns_True_For_Standard_Shell_Utility()
    {
        // Every supported platform has at least one of these on PATH.
        var found = GitVersionAttribute.IsOnPath("ls") || GitVersionAttribute.IsOnPath("cmd");
        Assert.True(found, "Expected `ls` (POSIX) or `cmd` (Windows) on PATH.");
    }

    // ─── GetValue type-check ────────────────────────────────────────────

    [Fact]
    public void GetValue_Throws_When_Target_Type_Is_Not_GitVersionInfo()
    {
        var attr = new GitVersionAttribute();
        // Fake a member reference; we just need to confirm the type guard fires
        // before any subprocess work happens.
        var member = typeof(GitVersionAttributeTests).GetField(nameof(SomeStringField),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            attr.GetValue(member, typeof(string)));
        Assert.Contains("GitVersionInfo", ex.Message);
    }

#pragma warning disable CS0414 // never assigned — used only for reflection in test above
    private string SomeStringField = "";
#pragma warning restore CS0414
}
