# Changelog

All notable changes to `Tamp.GitVersion` are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning 2.0.0](https://semver.org/).

Pre-1.0 versions may break public API freely between minor versions; the `0.x` line is intentionally a stabilization run.

## [Unreleased]

## [0.2.0] — pending — `[GitVersion]` value-injection attribute + `GitVersionInfo` result type (TAM-118)

### Added

- **`[GitVersion]` attribute** on `Tamp.GitVersion.V6.GitVersionAttribute` —
  `ValueInjection` attribute that populates a `GitVersionInfo`-typed member
  by invoking the GitVersion 6.x CLI at bind time, capturing JSON output,
  and parsing into a strongly-typed record. Parallels the existing
  `[Solution]` / `[GitRepository]` / `[NuGetPackage]` injection surface.

  ```csharp
  class Build : TampBuild
  {
      [GitVersion] readonly GitVersionInfo Version = null!;

      Target Image => _ => _.Executes(() =>
          Docker.Build(s => s.SetTag($"myapp:{Version.SemVer}-{Version.ShortSha}")));
  }
  ```

  Optional attribute properties:
  - **`Executable`** — override the CLI name (default: autoprobe
    `dotnet-gitversion` then `gitversion`).
  - **`WorkingDirectory`** — run in a directory other than the build's
    current working directory.
  - **`NoFetch`** — defaults `true` so bind time doesn't hit the network;
    set `false` if your build needs GitVersion to fetch tags.
  - **`TimeoutSeconds`** — defaults 30s. Bump for unusually large repos.

- **`GitVersionInfo` record** — typed result with the canonical GitVersion
  6.x output fields: Major/Minor/Patch, MajorMinorPatch, SemVer,
  FullSemVer, AssemblySemVer, InformationalVersion, BranchName /
  EscapedBranchName / Sha / ShortSha, PreReleaseTag family,
  CommitsSinceVersionSource, UncommittedChanges, CommitDate, plus a
  `Raw` dictionary that captures any fields the upstream CLI adds in
  patch versions.

### Why

HoldFast canary friction batch #17 (2026-05-13). Adopters expected
`[GitVersion]` to exist parallel to `[Solution]` / `[GitRepository]` —
the existing 0.1.x package shipped CLI-only, so adopters wrote
speculative code against a non-existent injection surface, found nothing,
and backed out to a short-SHA tag. 0.2.0 ships the missing surface so the
most common adoption case (image-tag = SemVer + ShortSha from git history)
is a single declaration.

### Executable resolution

Autoprobe order:
1. `Executable` attribute property if set.
2. `dotnet-gitversion` on PATH (canonical install via
   `dotnet tool install -g GitVersion.Tool`).
3. `gitversion` on PATH (legacy / Chocolatey install).

If none resolve, the binder throws `InvalidOperationException` with the
install hint. Combined with Tamp.Core 1.9.0's `tolerateInjectionFailures`
mode for `--list` (HoldFast friction #20), `tamp --list` still works when
GitVersion isn't yet installed.

### Tests

18 new tests in `GitVersionAttributeTests` — JSON-to-record parsing
across the full canonical output schema, tolerance for informational
preamble lines, error wrapping (no-JSON, malformed-JSON), unknown-field
capture in the `Raw` dictionary, PATH probing heuristic, attribute
default values, `GetValue` type-guard. Subprocess invocation itself is
covered by the existing integration test project.

## [0.1.1] — 2026-05-11

### Added — TAM-161

- Object-init overloads on every GitVersion wrapper (TAM-161 satellite fanout). `GitVersion.Run` now accepts `(Tool, GitVersionSettings)` alongside the existing `(Tool, Action<GitVersionSettings>)` configurer form. Both styles produce byte-equal `CommandPlan`s. Fluent stays canonical in docs; object-init is available for consumers who prefer the C# initializer shape.
