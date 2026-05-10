# Tamp.GitVersion

GitVersion CLI wrapper for [Tamp](https://github.com/tamp-build/tamp).

| Package | GitVersion | Status |
|---|---|---|
| [`Tamp.GitVersion.V6`](src/Tamp.GitVersion.V6) | 6.x | live |

Derives a SemVer product version from a GitFlow / GitHub-flow / trunk-based
git history. The wrapper covers the full CLI surface — outputs (json / file /
buildserver / dotenv), single-variable extraction, custom format strings,
config + override-config, assembly-info / project-file / WiX patching, and
remote-repo args.

## Why a separate repo

GitVersion ships independently of .NET on its own cadence (V5 → V6 in 2024,
patches every couple months). Per the Tamp satellite-repo convention,
third-party tools with their own release schedule live outside main.

## Quick example

```csharp
using Tamp;
using Tamp.GitVersion.V6;

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
            // ... runner spawns the plan, stdout has the value ...
        });

    Target Compile => _ => _
        .DependsOn(nameof(ResolveVersion))
        .Executes(() => DotNet.Build(s => s
            .SetProperty("Version", _semVer!)));
}
```

## License

[MIT](LICENSE) — same as `tamp` core.
