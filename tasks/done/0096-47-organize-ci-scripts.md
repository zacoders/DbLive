---
type: chore
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# Split the CI scripts into an outline and steps

## Release note
None. CI scripts only.

## Problem
`.github/scripts/release-notes.cs` and `.github/scripts/package-tests.cs` are about 340 lines each: one long run of top-level statements that does every step inline, followed by a pile of helpers. To see what a script does, the maintainer reads it from top to bottom; it is hard to follow.

## Fix
Keep them file-based apps, so the commands in CI and in the docs stay the same. The script file becomes the outline: it reads the arguments and calls the steps in order, with a list of its files in the comment at the top. Each step goes to a file of its own in the folder of the same name, included with `#:include`. No change in behavior.

## Docs to update
- `Development.md`, new section "Scripts"

## Done when
- [x] `release-notes.cs` split; the same output and exit code as before in the preview, with `--check` and with `--version`
- [x] `package-tests.cs` split; all checks pass against packages of a local build, the same as before
- [x] `Development.md` describes the layout

## Notes
- `#:include` works in SDK 10.0.303 and 10.0.402 (verified), also with a path into a subfolder; CI installs the latest `10.0.x`. VS Code resolves the included files too: no errors in the Problems panel.
- A file with `#:` directives that does not start with `#!` gets warning CA2266 on a fresh build, so both scripts start with `#!/usr/bin/env dotnet` (verified: the warning is gone).
- package-tests: the test projects reference the packages with the version itself instead of a `$(DbLiveVersion)` property, the projects are created by `workspace.Project(name, packages: [...])`, and the expectations read `ExpectScripts(folder, [...])`, `ExpectNoScripts()`, `ExpectBuildError(message)`. The argument parsing is shorter: the script takes only `--packages <folder>`.

## Result
- `release-notes.cs` (70 lines) is the outline: what is released, the version, the checks, the notes. The steps are in `release-notes/`: `ClosedTask.cs` (the tasks and their check), `VersionBump.cs` (the needed and the given bump, their check, the next version), `Compatibility.cs` (MinCompatibleVersion), `ReleaseNotes.cs` (the Markdown), `Git.cs`, `Options.cs`.
- `package-tests.cs` (100 lines) lists the checks: six projects and the template for two providers. The mechanics are in `package-tests/`: `Workspace.cs` (the temporary folder, nuget.config, the .csproj), `TestProject.cs` (build, embedded scripts, metadata), `Template.cs` (install, create, references, tests), `Report.cs` (the output and the failures), `Dotnet.cs`.
- Tests, verified: release-notes gives the same output, errors and exit code as before in four runs (preview; `--check` with a breaking title; `--check` with a plain title; `--version 9.9.9`). package-tests, old and new, against packages packed locally: the same output, all checks passed, the template tests included (Docker). The failure path: without the template package the new script reports the failure at the end, keeps the folder and exits with 1.
