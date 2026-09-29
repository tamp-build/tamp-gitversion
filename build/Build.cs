using Tamp;
using Tamp.NetCli.V10;
using Tamp.Telegram;
using Tamp.Components;
using Tamp.Components.NetCli.V10;

/// <summary>
/// tamp-gitversion's self-hosted build script. Drives the
/// restore / build / test / pack / push pipeline through Tamp itself
/// — full dogfood of the published Tamp.Core + Tamp.NetCli.V10.
/// </summary>
class Build : TampBuild, IDotNetPack
{
    public static int Main(string[] args) => Execute<Build>(args);

    // TAM-227 — Telegram failure notify. Pulls TELEGRAM_BOT_TOKEN /
    // TELEGRAM_CHAT_ID / TELEGRAM_BUILD_LABEL from the environment;
    // returns null when missing, framework silently skips null reporters.
    [BuildReporter] readonly IBuildReporter? TelegramNotify =
        TelegramBuildReporter.FromEnvironment();

    [Parameter("Build configuration")]
    public Configuration Configuration { get; set; } = IsLocalBuild ? Configuration.Debug : Configuration.Release;


    [Solution] public Solution Solution { get; set; } = null!;
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

    public AbsolutePath ArtifactsDirectory => Artifacts;

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

    Target Test => _ => _
        .DependsOn(nameof(ICompile.Compile))
        .Description("Run the unit test suite (does NOT run integration tests — those need dotnet-gitversion installed).")
        .Executes(() => DotNet.Test(s => s
            .SetProject(RootDirectory / "tests" / "Tamp.GitVersion.V6.Tests" / "Tamp.GitVersion.V6.Tests.csproj")
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")
            .AddDataCollector("XPlat Code Coverage")
            .SetSettings((RootDirectory / "build" / "coverlet.runsettings").Value)
            .SetResultsDirectory(Artifacts / "test-results")));

    Target Push => _ => _
        .DependsOn(nameof(IPack.Pack))
        .Description("Push every nupkg in ./artifacts to nuget.org. Driven by tag-triggered CI.")
        .Requires(() => NuGetApiKey != null)
        .Executes(() => Artifacts.GlobFiles("*.nupkg")
            .Select(p => DotNet.NuGetPush(s => s
                .SetPackagePath(p)
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetApiKey)
                .SetSkipDuplicate(true))));

    Target Ci => _ => _
        .DependsOn(nameof(Info), nameof(Clean), nameof(Test), nameof(IPack.Pack))
        .Description("Full CI pipeline: print info, clean, restore, build, test, pack. Push is a separate target run on release tags only.");

    Target Default => _ => _
        .DependsOn(nameof(ICompile.Compile))
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
        .Before(nameof(ICompile.Compile))
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
