using Tamp;
using Tamp.NetCli.V10;

/// <summary>
/// tamp-gitversion's self-hosted build script. Drives the
/// restore / build / test / pack / push pipeline through Tamp itself
/// — full dogfood of the published Tamp.Core + Tamp.NetCli.V10.
/// </summary>
class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Parameter("Build configuration")]
    Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Package version override (resolved from CI tag, e.g. v0.1.0 → 0.1.0)", EnvironmentVariable = "PACKAGE_VERSION")]
#pragma warning disable CS0649 // Set by reflection via [Parameter] binding.
    readonly string? Version;
#pragma warning restore CS0649

    [Solution] readonly Solution Solution = null!;
    [GitRepository] readonly GitRepository Git = null!;

    [Secret("NuGet API key for nuget.org publishing", EnvironmentVariable = "NUGET_API_KEY")]
    readonly Secret NuGetApiKey = null!;

    AbsolutePath Artifacts => RootDirectory / "artifacts";

    Target Info => _ => _
        .Description("Print build context (branch, commit, configuration) — useful at the top of CI logs.")
        .Executes(() =>
        {
            Console.WriteLine($"  Branch:        {Git.Branch ?? "<detached>"}");
            Console.WriteLine($"  Commit:        {Git.Commit[..7]}");
            Console.WriteLine($"  Configuration: {Configuration}");
            Console.WriteLine($"  Solution:      {Solution.Name} ({Solution.Projects.Count} project{(Solution.Projects.Count == 1 ? "" : "s")})");
            Console.WriteLine($"  Local build:   {IsLocalBuild}");
        });

    Target Clean => _ => _
        .TopLevel()
        .Description("Delete bin/obj across the tree and the artifacts directory.")
        .Executes(() =>
        {
            foreach (var d in RootDirectory.GlobDirectories("**/bin", "**/obj"))
                d.Delete();
            Artifacts.Delete();
        });

    Target Restore => _ => _
        .Description("dotnet restore the solution. CI uses TampCoreMode=package so Tamp.Core comes from nuget.org.")
        .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

    Target Compile => _ => _
        .TopLevel()
        .DependsOn(nameof(Restore))
        .Description("dotnet build the solution.")
        .Executes(() => DotNet.Build(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .TopLevel()
        .DependsOn(nameof(Compile))
        .Description("Run the unit test suite (does NOT run integration tests — those need dotnet-gitversion installed).")
        .Executes(() => DotNet.Test(s => s
            .SetProject(RootDirectory / "tests" / "Tamp.GitVersion.V6.Tests" / "Tamp.GitVersion.V6.Tests.csproj")
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")
            .SetResultsDirectory(Artifacts / "test-results")));

    Target Pack => _ => _
        .TopLevel()
        .DependsOn(nameof(Test))
        .Description("Pack the Tamp.GitVersion.V6 NuGet package into ./artifacts.")
        .Executes(() => DotNet.Pack(s =>
        {
            s.SetProject(RootDirectory / "src" / "Tamp.GitVersion.V6" / "Tamp.GitVersion.V6.csproj");
            s.SetConfiguration(Configuration);
            s.SetNoBuild(true);
            s.SetOutput(Artifacts);
            if (!string.IsNullOrEmpty(Version)) s.SetProperty("Version", Version);
        }));

    Target Push => _ => _
        .TopLevel()
        .DependsOn(nameof(Pack))
        .Description("Push every nupkg in ./artifacts to nuget.org. Driven by tag-triggered CI.")
        .Requires(() => NuGetApiKey != null)
        .Executes(() => Artifacts.GlobFiles("*.nupkg")
            .Select(p => DotNet.NuGetPush(s => s
                .SetPackagePath(p)
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetApiKey)
                .SetSkipDuplicate(true))));

    Target Ci => _ => _
        .TopLevel()
        .DependsOn(nameof(Info), nameof(Clean), nameof(Pack))
        .Description("Full CI pipeline: print info, clean, restore, build, test, pack. Push is a separate target run on release tags only.");

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");
}
