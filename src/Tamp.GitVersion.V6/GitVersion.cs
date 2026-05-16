namespace Tamp.GitVersion.V6;

/// <summary>
/// Wrapper for the GitVersion 6.x CLI (the <c>gitversion</c> /
/// <c>dotnet-gitversion</c> global tool — same binary, two install
/// shapes).
/// </summary>
/// <remarks>
/// <para>Resolve the tool via <c>[NuGetPackage]</c>:</para>
/// <code>
/// [NuGetPackage("GitVersion.Tool", Version = "6.7.0", ExecutableName = "dotnet-gitversion")]
/// readonly Tool GitVersionTool;
/// </code>
/// <para>
/// GitVersion's CLI uses single-slash flags (<c>/output</c>, etc.) — this
/// is a tool quirk, not a wrapper choice; the wrapper emits exactly what
/// the tool expects.
/// </para>
/// </remarks>
public static class GitVersion
{
    /// <summary>
    /// Run <c>gitversion</c>. Pass <c>null</c> as the configurer to run
    /// against the current directory and emit JSON to stdout.
    /// </summary>
    public static CommandPlan Run(Tool tool, Action<GitVersionSettings>? configure = null)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new GitVersionSettings();
        configure?.Invoke(s);
        return Plan(tool, s);
    }

    private static CommandPlan Plan(Tool tool, GitVersionSettings s)
    {
        var args = new List<string>();

        if (!string.IsNullOrEmpty(s.TargetPath)) { args.Add("/targetpath"); args.Add(s.TargetPath!); }
        foreach (var o in s.Outputs) { args.Add("/output"); args.Add(OutputToken(o)); }
        if (!string.IsNullOrEmpty(s.OutputFile)) { args.Add("/outputfile"); args.Add(s.OutputFile!); }
        if (!string.IsNullOrEmpty(s.ShowVariable)) { args.Add("/showvariable"); args.Add(s.ShowVariable!); }
        if (!string.IsNullOrEmpty(s.Format)) { args.Add("/format"); args.Add(s.Format!); }
        if (!string.IsNullOrEmpty(s.LogFile)) { args.Add("/l"); args.Add(s.LogFile!); }
        if (!string.IsNullOrEmpty(s.ConfigFile)) { args.Add("/config"); args.Add(s.ConfigFile!); }
        if (s.ShowConfig) args.Add("/showconfig");
        foreach (var (k, v) in s.OverrideConfig)
        {
            args.Add("/overrideconfig");
            args.Add($"{k}={v}");
        }
        if (s.NoCache) args.Add("/nocache");
        if (s.NoNormalize) args.Add("/nonormalize");
        if (s.AllowShallow) args.Add("/allowshallow");
        if (s.Verbosity is { } verb) { args.Add("/verbosity"); args.Add(verb.ToString()); }
        if (s.Diagnostic) args.Add("/diag");

        if (s.UpdateAssemblyInfo)
        {
            args.Add("/updateassemblyinfo");
            foreach (var f in s.AssemblyInfoFiles) args.Add(f);
        }
        if (s.UpdateProjectFiles) args.Add("/updateprojectfiles");
        if (s.EnsureAssemblyInfo) args.Add("/ensureassemblyinfo");
        if (s.UpdateWixVersionFile) args.Add("/updatewixversionfile");

        if (!string.IsNullOrEmpty(s.Url)) { args.Add("/url"); args.Add(s.Url!); }
        if (!string.IsNullOrEmpty(s.Branch)) { args.Add("/b"); args.Add(s.Branch!); }
        if (!string.IsNullOrEmpty(s.Username)) { args.Add("/u"); args.Add(s.Username!); }
        // TODO: extract Reveal into GitVersionPasswordSettings to satisfy TAMP004 cleanly.
#pragma warning disable TAMP004
        if (s.Password is { } pw) { args.Add("/p"); args.Add(pw.Reveal()); }
#pragma warning restore TAMP004
        if (!string.IsNullOrEmpty(s.Commit)) { args.Add("/c"); args.Add(s.Commit!); }
        if (!string.IsNullOrEmpty(s.DynamicRepoLocation)) { args.Add("/dynamicRepoLocation"); args.Add(s.DynamicRepoLocation!); }
        if (s.NoFetch) args.Add("/nofetch");

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(s.EnvironmentVariables),
            WorkingDirectory = s.WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = s.Password is null ? Array.Empty<Secret>() : new[] { s.Password },
        };
    }

    private static string OutputToken(GitVersionOutput o) => o switch
    {
        GitVersionOutput.Json => "json",
        GitVersionOutput.File => "file",
        GitVersionOutput.BuildServer => "buildserver",
        GitVersionOutput.DotEnv => "dotenv",
        _ => throw new ArgumentOutOfRangeException(nameof(o), o, "Unknown output format."),
    };

    // ---- Object-init overloads (0.1.1+, TAM-161) ----
    // Two equivalent authoring styles; both produce identical CommandPlans. Fluent
    // stays canonical in docs and `tamp init` templates; object-init available for
    // consumers who prefer the C# initializer shape.
    //
    //     GitVersion.Run(GitVersionTool, new() { TargetPath = RootDirectory, ShowVariable = "SemVer" });
    //
    // is equivalent to:
    //
    //     GitVersion.Run(GitVersionTool, s => s.SetTargetPath(RootDirectory).SetShowVariable("SemVer"));

    public static CommandPlan Run(Tool tool, GitVersionSettings settings)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        return Plan(tool, settings);
    }
}
