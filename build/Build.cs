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

    // Tamp.Core 1.0.0's [Secret] attribute is declared but the resolver
    // isn't wired up yet (ParameterBinder explicitly excludes secrets).
    // Resolve from env directly until Tamp.Core 1.0.1 lands the
    // proper SecretBinder. The Secret type still gives us redaction in
    // any logged output the runner sees.
    // Bound by SecretBinder from NUGET_API_KEY env var (TAM-78,
    // Tamp.Core 1.0.1). CI masking via TampBuild.RegisterSecretForCiMasking.
    [Secret("NuGet API key", EnvironmentVariable = "NUGET_API_KEY")]
    readonly Secret NuGetApiKey = null!;

    AbsolutePath Artifacts => RootDirectory / "artifacts";

    Target Info => _ => _
        .Description("Print build context (branch, commit, configuration) — useful at the top of CI logs.")
        .Executes(() =>
        {
            Console.WriteLine($"  Branch:        {Git.Branch ?? "<detached>"}");
            Console.WriteLine($"  Commit:        {Git.Commit[..7]}");
            Console.WriteLine($"  Configuration: {Configuration}");
        });

    Target Clean => _ => _
        .Description("Delete bin/obj and the artifacts directory.")
        .Executes(() => CleanArtifacts());

    Target Restore => _ => _
        .Description("dotnet restore the solution. CI uses TampCoreMode=package so Tamp.Core comes from nuget.org.")
        .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

    Target Compile => _ => _
        .DependsOn(nameof(Restore))
        .Description("dotnet build the solution.")
        .Executes(() => DotNet.Build(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .DependsOn(nameof(Compile))
        .Description("Run the unit test suite (does NOT run integration tests — those need dotnet-gitversion installed).")
        .Executes(() => DotNet.Test(s => s
            .SetProject(RootDirectory / "tests" / "Tamp.GitVersion.V6.Tests" / "Tamp.GitVersion.V6.Tests.csproj")
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")
            .AddDataCollector("XPlat Code Coverage")
            .SetSettings((RootDirectory / "build" / "coverlet.runsettings").Value)
            .SetResultsDirectory(Artifacts / "test-results")));

    Target Pack => _ => _
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
        .DependsOn(nameof(Info), nameof(Clean), nameof(Pack))
        .Description("Full CI pipeline: print info, clean, restore, build, test, pack. Push is a separate target run on release tags only.");

    Target Default => _ => _
        .DependsOn(nameof(Compile))
        .Description("Local-developer default: restore + build the solution.");

    // ----- Sonar (TAM-17) -----

    [NuGetPackage("dotnet-sonarscanner", Version = "10.4.1")]
    readonly Tool SonarTool = null!;


    [Secret("SonarQube token", EnvironmentVariable = "SONAR_TOKEN")]


    readonly Secret SonarToken = null!;

    [Parameter("Sonar host URL", EnvironmentVariable = "SONAR_HOST_URL")]
    readonly string SonarHostUrl = "https://sonar.brewingcoder.com";

    [Parameter("Sonar project key")]
    readonly string SonarProjectKey = "tamp-build_tamp-gitversion";

    Target SonarBegin => _ => _
        .Description("Initialize the SonarScanner pre-build phase.")
        .Before(nameof(Compile))
        .Requires(() => SonarToken != null)
        .Executes(() => Tamp.SonarScanner.V10.SonarScanner.Begin(SonarTool, s =>
        {
            s.SetProjectKey(SonarProjectKey);
            s.SetHostUrl(SonarHostUrl);
            s.SetToken(SonarToken);
            s.SetProperty("sonar.cs.vstest.reportsPaths", $"{(Artifacts / "test-results").Value}/**/*.trx");
            s.SetProperty("sonar.cs.opencover.reportsPaths", $"{(Artifacts / "test-results").Value}/**/coverage.opencover.xml");

            s.SetProperty("sonar.coverage.exclusions", "tests/**,build/**,samples/**");

            s.SetProperty("sonar.exclusions", "**/bin/**,**/obj/**,artifacts/**,build/**,docs/**,samples/**");
        }));

    Target SonarEnd => _ => _
        .Description("Finalize SonarScanner and submit results to the server.")
        .DependsOn(nameof(Test))
        .Requires(() => SonarToken != null)
        .Executes(() => Tamp.SonarScanner.V10.SonarScanner.End(SonarTool, s => s.SetToken(SonarToken)));

    Target Sonar => _ => _
        .DependsOn(nameof(SonarBegin), nameof(SonarEnd))
        .Description("End-to-end Sonar scan: Begin (before Compile) → Compile → Test → End. Requires SONAR_TOKEN.");

}
