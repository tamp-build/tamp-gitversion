namespace Tamp.GitVersion.V6;

/// <summary>
/// Output format for <c>gitversion</c>. Maps to <c>/output</c>.
/// Multiple may be specified per run.
/// </summary>
public enum GitVersionOutput
{
    /// <summary>JSON to stdout (the default when no <c>/output</c> is supplied).</summary>
    Json,
    /// <summary>Write the JSON to the path given by <c>/outputfile</c>.</summary>
    File,
    /// <summary>Emit build-server environment variables (TeamCity / Azure Pipelines).</summary>
    BuildServer,
    /// <summary>Emit a <c>.env</c>-style block (one <c>KEY=value</c> per line).</summary>
    DotEnv,
}

/// <summary>
/// Verbosity level. Maps to <c>/verbosity</c>. Default is Normal.
/// </summary>
public enum GitVersionVerbosity
{
    Quiet,
    Minimal,
    Normal,
    Verbose,
    Diagnostic,
}

/// <summary>
/// Settings for a <c>gitversion</c> invocation.
/// </summary>
/// <remarks>
/// <para>
/// GitVersion's CLI uses single-slash flags (<c>/output</c>, <c>/showvariable</c>)
/// rather than the conventional <c>--double-dash</c>. The wrapper emits
/// the slash form unchanged. The runner doesn't care; this is just how
/// the tool's argument parser was built.
/// </para>
/// <para>
/// Most build scripts call this once at the top of the run and stash the
/// result on a typed property. The two most-useful invocation shapes:
/// </para>
/// <code>
/// // 1. Get the full JSON (parse it yourself).
/// GitVersion.Run(tool, s => s.SetTargetPath(RootDirectory))
///
/// // 2. Get a single variable straight to stdout.
/// GitVersion.Run(tool, s => s
///     .SetTargetPath(RootDirectory)
///     .SetShowVariable("SemVer"))
/// </code>
/// </remarks>
public sealed class GitVersionSettings
{
    /// <summary>Path to the git working tree. Maps to <c>/targetpath</c>.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Output formats. Maps to one or more <c>/output &lt;format&gt;</c>. Defaults to JSON when empty.</summary>
    public List<GitVersionOutput> Outputs { get; } = [];

    /// <summary>Path to write the JSON when <see cref="Outputs"/> contains <c>File</c>. Maps to <c>/outputfile</c>.</summary>
    public string? OutputFile { get; set; }

    /// <summary>Emit only the named variable (e.g. <c>SemVer</c>, <c>NuGetVersionV2</c>). Maps to <c>/showvariable</c>.</summary>
    public string? ShowVariable { get; set; }

    /// <summary>C#-style format string (e.g. <c>{Major}.{Minor}.{Patch}</c>). Maps to <c>/format</c>.</summary>
    public string? Format { get; set; }

    /// <summary>Path to log file, or <c>"console"</c> to emit to stdout. Maps to <c>/l</c>.</summary>
    public string? LogFile { get; set; }

    /// <summary>Path to the GitVersion config file. Defaults to <c>GitVersion.yml</c> in <see cref="TargetPath"/>. Maps to <c>/config</c>.</summary>
    public string? ConfigFile { get; set; }

    /// <summary>Print the effective config and exit. Maps to <c>/showconfig</c>.</summary>
    public bool ShowConfig { get; set; }

    /// <summary>Inline config overrides — emitted as <c>/overrideconfig key=value</c> repeated.</summary>
    /// <remarks>GitVersion 6 only honors <c>tag-prefix</c> via this mechanism today; other keys are silently ignored.</remarks>
    public Dictionary<string, string> OverrideConfig { get; } = new();

    /// <summary>Bypass the version cache. Maps to <c>/nocache</c>.</summary>
    public bool NoCache { get; set; }

    /// <summary>Skip the build-server normalize step. Maps to <c>/nonormalize</c>.</summary>
    public bool NoNormalize { get; set; }

    /// <summary>Tolerate a shallow clone. Maps to <c>/allowshallow</c>. Not recommended unless you know the clone has the commits GitVersion needs.</summary>
    public bool AllowShallow { get; set; }

    /// <summary>Verbosity. Maps to <c>/verbosity</c>.</summary>
    public GitVersionVerbosity? Verbosity { get; set; }

    /// <summary>Run with extra diagnostic output. Pair with <see cref="LogFile"/>. Maps to <c>/diag</c>.</summary>
    public bool Diagnostic { get; set; }

    /// <summary>Recursively update <c>AssemblyInfo.cs</c> files in the repo. Maps to <c>/updateassemblyinfo</c>.</summary>
    public bool UpdateAssemblyInfo { get; set; }

    /// <summary>Specific assembly-info files to update. Empty = recurse from <see cref="TargetPath"/>.</summary>
    public List<string> AssemblyInfoFiles { get; } = [];

    /// <summary>Recursively update SDK-style project files. Maps to <c>/updateprojectfiles</c>.</summary>
    public bool UpdateProjectFiles { get; set; }

    /// <summary>Create the assembly-info file if it doesn't exist. Maps to <c>/ensureassemblyinfo</c>.</summary>
    public bool EnsureAssemblyInfo { get; set; }

    /// <summary>Write a <c>GitVersion_WixVersion.wxi</c> with all variables. Maps to <c>/updatewixversionfile</c>.</summary>
    public bool UpdateWixVersionFile { get; set; }

    // ----- Remote-repo args -----

    /// <summary>Remote git URL. Maps to <c>/url</c>.</summary>
    public string? Url { get; set; }

    /// <summary>Branch on the remote. Maps to <c>/b</c>.</summary>
    public string? Branch { get; set; }

    /// <summary>Username for the remote. Maps to <c>/u</c>.</summary>
    public string? Username { get; set; }

    /// <summary>Password / token for the remote. Pass as <see cref="Secret"/> so it's redacted. Maps to <c>/p</c>.</summary>
    public Secret? Password { get; set; }

    /// <summary>Specific commit to compute the version at. Maps to <c>/c</c>.</summary>
    public string? Commit { get; set; }

    /// <summary>Override where dynamic clones go. Maps to <c>/dynamicRepoLocation</c>.</summary>
    public string? DynamicRepoLocation { get; set; }

    /// <summary>Skip the implicit <c>git fetch</c>. Maps to <c>/nofetch</c>.</summary>
    public bool NoFetch { get; set; }

    // ----- Process -----

    /// <summary>Working directory of the spawned process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Per-invocation environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public GitVersionSettings SetTargetPath(string? path) { TargetPath = path; return this; }
    public GitVersionSettings AddOutput(GitVersionOutput output) { Outputs.Add(output); return this; }
    public GitVersionSettings SetOutputFile(string? path) { OutputFile = path; return this; }
    public GitVersionSettings SetShowVariable(string? name) { ShowVariable = name; return this; }
    public GitVersionSettings SetFormat(string? format) { Format = format; return this; }
    public GitVersionSettings SetLogFile(string? path) { LogFile = path; return this; }
    public GitVersionSettings SetConfigFile(string? path) { ConfigFile = path; return this; }
    public GitVersionSettings SetShowConfig(bool v = true) { ShowConfig = v; return this; }
    public GitVersionSettings SetOverrideConfig(string key, string value) { OverrideConfig[key] = value; return this; }
    public GitVersionSettings SetNoCache(bool v = true) { NoCache = v; return this; }
    public GitVersionSettings SetNoNormalize(bool v = true) { NoNormalize = v; return this; }
    public GitVersionSettings SetAllowShallow(bool v = true) { AllowShallow = v; return this; }
    public GitVersionSettings SetVerbosity(GitVersionVerbosity v) { Verbosity = v; return this; }
    public GitVersionSettings SetDiagnostic(bool v = true) { Diagnostic = v; return this; }
    public GitVersionSettings SetUpdateAssemblyInfo(bool v = true) { UpdateAssemblyInfo = v; return this; }
    public GitVersionSettings AddAssemblyInfoFile(string path) { AssemblyInfoFiles.Add(path); return this; }
    public GitVersionSettings SetUpdateProjectFiles(bool v = true) { UpdateProjectFiles = v; return this; }
    public GitVersionSettings SetEnsureAssemblyInfo(bool v = true) { EnsureAssemblyInfo = v; return this; }
    public GitVersionSettings SetUpdateWixVersionFile(bool v = true) { UpdateWixVersionFile = v; return this; }
    public GitVersionSettings SetUrl(string? url) { Url = url; return this; }
    public GitVersionSettings SetBranch(string? branch) { Branch = branch; return this; }
    public GitVersionSettings SetUsername(string? user) { Username = user; return this; }
    public GitVersionSettings SetPassword(Secret password) { Password = password; return this; }
    public GitVersionSettings SetCommit(string? commit) { Commit = commit; return this; }
    public GitVersionSettings SetDynamicRepoLocation(string? path) { DynamicRepoLocation = path; return this; }
    public GitVersionSettings SetNoFetch(bool v = true) { NoFetch = v; return this; }
    public GitVersionSettings SetWorkingDirectory(string? cwd) { WorkingDirectory = cwd; return this; }
    public GitVersionSettings SetEnvironmentVariable(string name, string value) { EnvironmentVariables[name] = value; return this; }
}
