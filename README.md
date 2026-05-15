# Tamp.GitVersion

GitVersion CLI wrapper for [Tamp](https://github.com/tamp-build/tamp).

| Package | GitVersion | Status |
|---|---|---|
| [`Tamp.GitVersion.V6`](src/Tamp.GitVersion.V6) | 6.x | live (0.1.0) |

Derives a SemVer product version from a GitFlow / GitHub-flow / trunk-based
git history. Full CLI surface — outputs (json / file / buildserver /
dotenv), single-variable extraction, custom format strings, config +
override-config, assembly-info / project-file / WiX patching, and
remote-repo args.

Requires `Tamp.Core ≥ 1.0.0`.

This was the **first satellite released through the Tamp dogfood
pipeline** — `dotnet tamp Ci` + `dotnet tamp Push` in
[`.github/workflows/release.yml`](.github/workflows/release.yml).

## Why a separate repo

GitVersion ships independently of .NET on its own cadence (V5 → V6 in
2024, patches every couple months). Per the satellite-repo convention,
third-party tools with their own release schedule live outside main.

## Install

In your build script's `Directory.Packages.props`:

```xml
<PackageVersion Include="Tamp.GitVersion.V6" Version="0.1.0" />
```

In `build/Build.csproj`:

```xml
<PackageReference Include="Tamp.GitVersion.V6" />
```

## Minimal adoption snippet (0.2.0+) — `[GitVersion]` value-injection

```csharp
using Tamp;
using Tamp.GitVersion.V6;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [GitVersion] readonly GitVersionInfo Version = null!;
    //  ↑ GitVersion 6.x runs once at bind time; result is typed + cached.
    //    Default executable: dotnet-gitversion (then gitversion). Override
    //    via [GitVersion(Executable = "...")] if needed.

    Target Image => _ => _.Executes(() =>
        Docker.Build(s => s
            .SetTag($"myapp:{Version.SemVer}-{Version.ShortSha}")));
}
```

`GitVersionInfo` carries the full GitVersion 6.x output schema: `Major` /
`Minor` / `Patch`, `MajorMinorPatch`, `SemVer`, `FullSemVer`,
`AssemblySemVer`, `InformationalVersion`, `BranchName` / `EscapedBranchName` /
`Sha` / `ShortSha`, `PreReleaseTag` family, `CommitsSinceVersionSource`,
`UncommittedChanges`, `CommitDate`, plus a `Raw` dictionary that captures
any new fields the upstream CLI adds in patch versions.

**This is the recommended shape for 0.2.0+ adopters.** Lower-level
`GitVersion.Run(...)` CLI wrapper (below) stays available for adopters who
need the JSON output as a typed `CommandPlan` (build-graph traceability,
explicit working directory, etc).

## Quick example — explicit CLI wrapper (lower-level)

```csharp
using Tamp;
using Tamp.GitVersion.V6;
using Tamp.NetCli.V10;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [NuGetPackage("GitVersion.Tool", Version = "6.7.0", ExecutableName = "dotnet-gitversion")]
    readonly Tool GitVersionTool = null!;

    string? _semVer;

    Target ResolveVersion => _ => _
        .Description("Run GitVersion once and stash the SemVer for later targets.")
        .Executes(() =>
        {
            var plan = GitVersion.Run(GitVersionTool, s => s
                .SetTargetPath(RootDirectory)
                .SetShowVariable("SemVer"));
            // The runner captures stdout; parse and stash on _semVer.
        });

    Target Compile => _ => _
        .DependsOn(nameof(ResolveVersion))
        .Executes(() => DotNet.Build(s => s
            .SetProperty("Version", _semVer!)));
}
```

## See also

- [tamp](https://github.com/tamp-build/tamp) — the core framework
- [GitVersion docs](https://gitversion.net/) — variable reference, branch config

## Settings authoring style

Examples above use the fluent `Set*`-chain shape. Every wrapper verb also accepts a `new XxxSettings { ... }` object-init form — both produce identical `CommandPlan`s. The fluent shape stays canonical in docs and the `tamp init` template; opt into object-init scaffolding via `tamp init --settings-style=init`.

See [Build Script Authoring → Two authoring styles](https://github.com/tamp-build/tamp/wiki/Build-Script-Authoring#two-authoring-styles-for-wrapper-calls-120) on the wiki for the side-by-side comparison.

## License

[MIT](LICENSE) — same as `tamp` core. (GitVersion itself is MIT.)
