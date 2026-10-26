---
type: bug
priority: high
created: 2026-10-26
closed: 2026-10-26
---
# `SetProject` looks for scripts relative to the current directory

## Problem
The `<AssemblyName>.Scripts` path was resolved against `Environment.CurrentDirectory`, not the application folder. Started with `dotnet run`, as a Windows service or from a scheduler, the app did not find the scripts, so Getting Started did not work with `dotnet run`. In addition, `ProjectDir` metadata was required even with `useVsProjectPath: false`.

## Where
`src/DbLive/DbLiveBuilderExtensions.cs:46` (path), `:61` (`ProjectDir`).

## Fix
`Path.Combine(AppContext.BaseDirectory, name + ".Scripts")`. Read `ProjectDir` only when it is needed. Check `[SqlFact]` discovery and the demo tests: the test project gets a copy of the scripts through `ProjectReference`.

## Docs to update
- `docs/llms-full.txt` §3.3, §3.4, §19
- `docs/project/index.md` ("How DbLive finds the scripts")
- `docs/gettingstarted.md` (remove the `dotnet run` warning in step 7)
- `docs/deployment/index.md` (sentence "Start it from its output folder")

## Result
Fixed in 141e3cd. `SetProject` resolves scripts from `AppContext.BaseDirectory` whatever the working directory is; `ProjectDir` is read lazily, only for `useVsProjectPath` or `[SqlFact]` discovery (`IProjectPath` takes a factory). Tests: `DbLiveBuilderExtensionsTests`. Docs updated in the same commit.
