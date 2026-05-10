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

## Quick example

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

## License

[MIT](LICENSE) — same as `tamp` core. (GitVersion itself is MIT.)
