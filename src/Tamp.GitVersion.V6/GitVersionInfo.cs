using System.Text.Json.Serialization;

namespace Tamp.GitVersion.V6;

/// <summary>
/// Typed result of running GitVersion 6.x — the canonical fields adopters
/// reach for to derive build versions, docker image tags, and SemVer-driven
/// release branches.
/// </summary>
/// <remarks>
/// <para>
/// Populated by <see cref="GitVersionAttribute"/> at bind time by invoking
/// the GitVersion CLI with <c>/output json</c> and parsing the result.
/// Field set mirrors GitVersion 6.x's documented output schema; new fields
/// the upstream tool adds in patch versions remain accessible via the
/// <see cref="Raw"/> dictionary.
/// </para>
/// <para>
/// HoldFast canary friction #17 (2026-05-13). Adopters expected this type
/// to parallel <c>Solution</c> / <c>GitRepository</c> — built-in value-
/// injection result types — and were surprised it didn't exist.
/// </para>
/// </remarks>
public sealed class GitVersionInfo
{
    [JsonPropertyName("Major")]                       public int    Major { get; init; }
    [JsonPropertyName("Minor")]                       public int    Minor { get; init; }
    [JsonPropertyName("Patch")]                       public int    Patch { get; init; }

    [JsonPropertyName("PreReleaseTag")]               public string? PreReleaseTag { get; init; }
    [JsonPropertyName("PreReleaseTagWithDash")]       public string? PreReleaseTagWithDash { get; init; }
    [JsonPropertyName("PreReleaseLabel")]             public string? PreReleaseLabel { get; init; }
    [JsonPropertyName("PreReleaseNumber")]            public int?    PreReleaseNumber { get; init; }
    [JsonPropertyName("WeightedPreReleaseNumber")]    public int?    WeightedPreReleaseNumber { get; init; }

    [JsonPropertyName("BuildMetaData")]               public string? BuildMetaData { get; init; }
    [JsonPropertyName("BuildMetaDataPadded")]         public string? BuildMetaDataPadded { get; init; }
    [JsonPropertyName("FullBuildMetaData")]           public string? FullBuildMetaData { get; init; }

    [JsonPropertyName("MajorMinorPatch")]             public string  MajorMinorPatch { get; init; } = "";
    [JsonPropertyName("SemVer")]                      public string  SemVer { get; init; } = "";
    [JsonPropertyName("AssemblySemVer")]              public string  AssemblySemVer { get; init; } = "";
    [JsonPropertyName("AssemblySemFileVer")]          public string  AssemblySemFileVer { get; init; } = "";
    [JsonPropertyName("InformationalVersion")]        public string  InformationalVersion { get; init; } = "";
    [JsonPropertyName("FullSemVer")]                  public string  FullSemVer { get; init; } = "";

    [JsonPropertyName("BranchName")]                  public string  BranchName { get; init; } = "";
    [JsonPropertyName("EscapedBranchName")]           public string? EscapedBranchName { get; init; }
    [JsonPropertyName("Sha")]                         public string  Sha { get; init; } = "";
    [JsonPropertyName("ShortSha")]                    public string  ShortSha { get; init; } = "";

    [JsonPropertyName("CommitsSinceVersionSource")]   public int?   CommitsSinceVersionSource { get; init; }
    [JsonPropertyName("CommitsSinceVersionSourcePadded")] public string? CommitsSinceVersionSourcePadded { get; init; }
    [JsonPropertyName("UncommittedChanges")]          public int?   UncommittedChanges { get; init; }
    [JsonPropertyName("CommitDate")]                  public string? CommitDate { get; init; }
    [JsonPropertyName("VersionSourceSha")]            public string? VersionSourceSha { get; init; }

    /// <summary>
    /// Every field the upstream <c>gitversion</c> CLI emitted, including
    /// fields not surfaced as typed properties on this record. Useful when
    /// the upstream tool adds new fields between patch releases.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> Raw { get; init; } = new();

    public override string ToString() => SemVer;
}
