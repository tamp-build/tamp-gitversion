using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Tamp.GitVersion.V6;

/// <summary>
/// Value-injection attribute that populates a <see cref="GitVersionInfo"/>-typed
/// build-class member by invoking the GitVersion 6.x CLI at bind time.
/// </summary>
/// <remarks>
/// <para>
/// Adopter idiom (canonical):
/// </para>
/// <code>
/// class Build : TampBuild
/// {
///     [GitVersion] readonly GitVersionInfo Version = null!;
///
///     Target Image => _ => _.Executes(() =>
///         Docker.Build(s => s.SetTag($"myapp:{Version.SemVer}-{Version.ShortSha}")));
/// }
/// </code>
/// <para>
/// Resolution order for the GitVersion executable:
/// </para>
/// <list type="number">
///   <item>Explicit <see cref="Executable"/> property if set on the attribute.</item>
///   <item><c>dotnet-gitversion</c> on PATH (the canonical <c>dotnet tool install -g GitVersion.Tool</c> install).</item>
///   <item><c>gitversion</c> on PATH (the legacy / Chocolatey install).</item>
/// </list>
/// <para>
/// Runs <c>gitversion /output json /nofetch</c> against <see cref="WorkingDirectory"/>
/// (or the build's <c>RootDirectory</c> if unset). Captures stdout, parses as JSON,
/// populates the typed <see cref="GitVersionInfo"/>. Failures throw
/// <see cref="InvalidOperationException"/> — caught by <c>ParameterBinder</c>'s
/// list-mode tolerance (HoldFast friction #20) so <c>tamp --list</c> still works
/// when GitVersion isn't yet installed.
/// </para>
/// <para>
/// HoldFast canary friction #17 (2026-05-13). Adopters expected this attribute
/// parallel to <see cref="SolutionAttribute"/> / <see cref="GitRepositoryAttribute"/>;
/// it didn't exist. This adds the missing surface so the most common adoption
/// case (image tag = SemVer from git history) becomes a single declaration.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class GitVersionAttribute : ValueInjectionAttribute
{
    /// <summary>
    /// Override the GitVersion executable name. Default is autoprobe:
    /// <c>dotnet-gitversion</c> then <c>gitversion</c>.
    /// </summary>
    public string? Executable { get; init; }

    /// <summary>
    /// Working directory to run <c>gitversion</c> in. Default is the current
    /// working directory at bind time.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// When <c>true</c> (default), passes <c>/nofetch</c> to avoid hitting the
    /// network. Set <c>false</c> if your build genuinely needs GitVersion to
    /// fetch tags from the remote (rare; usually a CI checkout already has them).
    /// </summary>
    public bool NoFetch { get; init; } = true;

    /// <summary>
    /// Timeout for the GitVersion invocation. Default 30 seconds — enough for
    /// even a large repo on a slow CI runner.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    public override object? GetValue(System.Reflection.MemberInfo member, Type targetType)
    {
        if (targetType != typeof(GitVersionInfo))
            throw new InvalidOperationException(
                $"[GitVersion] member '{member.Name}' must be typed as {nameof(GitVersionInfo)}; got {targetType.Name}.");

        var executable = ResolveExecutable();
        var workingDir = WorkingDirectory ?? Environment.CurrentDirectory;

        var args = new List<string> { "/output", "json" };
        if (NoFetch) args.Add("/nofetch");

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException(
                $"[GitVersion] failed to start '{executable}'. Install GitVersion.Tool: " +
                "`dotnet tool install -g GitVersion.Tool`.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(TimeoutSeconds * 1000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException(
                $"[GitVersion] '{executable}' did not complete within {TimeoutSeconds}s. " +
                "Set TimeoutSeconds higher on the attribute if your repo is unusually large.");
        }
        proc.WaitForExit();   // drain output buffers

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"[GitVersion] '{executable}' exited with code {proc.ExitCode}. " +
                $"stderr:\n{stderr}\n\nstdout:\n{stdout}");

        return ParseGitVersionJson(stdout.ToString(), executable);
    }

    /// <summary>
    /// Parse <paramref name="rawStdout"/> from a <c>gitversion /output json</c>
    /// invocation into a <see cref="GitVersionInfo"/>. Tolerant of informational
    /// lines preceding the JSON object (which the CLI sometimes emits).
    /// Exposed <c>internal</c> for testability — bench-tested directly without
    /// needing to spin a real process.
    /// </summary>
    internal static GitVersionInfo ParseGitVersionJson(string rawStdout, string executableLabel)
    {
        var output = rawStdout?.Trim() ?? "";
        var jsonStart = output.IndexOf('{');
        if (jsonStart < 0)
            throw new InvalidOperationException(
                $"[GitVersion] '{executableLabel}' produced no JSON output. Raw stdout:\n{output}");

        try
        {
            return JsonSerializer.Deserialize<GitVersionInfo>(output[jsonStart..])
                ?? throw new InvalidOperationException(
                    $"[GitVersion] JSON parse returned null. Raw stdout:\n{output}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"[GitVersion] failed to parse JSON output. Raw stdout:\n{output}\n\nParse error: {ex.Message}",
                ex);
        }
    }

    private string ResolveExecutable()
    {
        if (!string.IsNullOrEmpty(Executable)) return Executable!;

        // Autoprobe: `dotnet-gitversion` is the canonical install (dotnet tool
        // install -g GitVersion.Tool). Fall back to `gitversion` for Chocolatey
        // / older installs.
        if (IsOnPath("dotnet-gitversion")) return "dotnet-gitversion";
        if (IsOnPath("gitversion")) return "gitversion";

        throw new InvalidOperationException(
            "[GitVersion] could not locate the GitVersion executable on PATH. " +
            "Install via `dotnet tool install -g GitVersion.Tool`, " +
            "or set the [GitVersion(Executable = \"...\")] property to point at it explicitly.");
    }

    /// <summary>
    /// True when <paramref name="command"/> is locatable on the current PATH.
    /// On Windows, probes against every <c>PATHEXT</c> suffix. Exposed
    /// <c>internal</c> for testability.
    /// </summary>
    internal static bool IsOnPath(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return false;

        var pathExt = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';')
            : new[] { "" };

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var ext in pathExt)
            {
                var candidate = Path.Combine(dir, command + ext);
                if (File.Exists(candidate)) return true;
            }
        }
        return false;
    }
}
