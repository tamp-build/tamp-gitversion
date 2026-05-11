# Changelog

All notable changes to `Tamp.GitVersion` are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning 2.0.0](https://semver.org/).

Pre-1.0 versions may break public API freely between minor versions; the `0.x` line is intentionally a stabilization run.

## [Unreleased]

## [0.1.1] — 2026-05-11

### Added — TAM-161

- Object-init overloads on every GitVersion wrapper (TAM-161 satellite fanout). `GitVersion.Run` now accepts `(Tool, GitVersionSettings)` alongside the existing `(Tool, Action<GitVersionSettings>)` configurer form. Both styles produce byte-equal `CommandPlan`s. Fluent stays canonical in docs; object-init is available for consumers who prefer the C# initializer shape.
