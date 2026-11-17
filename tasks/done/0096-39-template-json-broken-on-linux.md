---
type: bug
priority: high
created: 2026-11-03
closed: 2026-11-03
---
# The template package packed on Linux has a broken `template.json`

## Release note
None. The template package of 0096-30 is not released yet.

## Problem
The "Test packages" step of CI failed on the PR of #96 (build `1.5.1-pullrequest0098-0072`): `dotnet new dblive` exited with 103, "No templates or subcommands found matching: 'dblive'", for both providers. The same check passed on Windows (0096-30).

Verified in the `mcr.microsoft.com/dotnet/sdk:10.0` container (SDK 10.0.403): the package packed on Linux has all the files, but its `template.json` is not valid JSON. The target `DbLiveAddTemplates` wrote the copy with `WriteLinesToFile`, and MSBuild on Linux and macOS turns the backslashes of an item into slashes, so `"(provider == \"mssql\")"` became `"(provider == /"mssql/")"`. On Windows the backslashes stay.

`dotnet new install` does not fail on such a package. It prints "Failed to load template ... JsonReaderException" and "No templates were found in the package", and exits with 0, so the check printed "ok: installed" and failed only at `dotnet new dblive`.

## Where
- `src/DbLive.Templates/DbLive.Templates.csproj`, target `DbLiveAddTemplates`
- `.github/scripts/package-tests.cs`, the template install check

## Fix
- [x] Write the version into the copy of `template.json` with an inline task (`RoslynCodeTaskFactory`) that reads and writes the file in C#, so the text of the JSON does not go through MSBuild items.
- [x] The package check looks the template up by its short name after `dotnet new install` and fails there, with the install output.

## Done when
- [x] The package packed on Linux installs and creates a project
- [x] `package-tests.cs` passes on Linux
- [x] A package with no loadable template fails the install check

## Notes
2026-11-03, verified in the Linux container:
- Escaping the backslashes does not help: `.Replace('\', '%5C')` after `[MSBuild]::Escape` still wrote `/"`, and `%255C` wrote `%5C"` into the file.
- With the inline task the copy of `template.json` differs from the source only in the version line, on Linux and on Windows (`dotnet build`, then `dotnet pack --no-build`, as in CI). A source without the placeholder fails the pack with the same message as before.
- `dotnet new list dblive --debug:custom-hive <hive>` exits with 103 when the template did not load and with 0 when it did.
- The CI steps in the container (the seven packages packed from this branch, then `package-tests.cs`, with the Docker of the host): all checks passed; both generated solutions ran 3 tests each, all passed.
- The same run with the old `DbLive.Templates.csproj`: the check failed at "== Template" with "dotnet new install did not install the template dblive (exit code 0)" and the `JsonReaderException` of the install output, and did not go on to `dotnet new dblive`.

## Result
- `DbLive.Templates.csproj`: the task `DbLiveSetTemplateVersion` replaces `WriteLinesToFile` and the `Error` before it; the check for the placeholder moved into the task.
- `package-tests.cs`: after `dotnet new install` the check runs `dotnet new list dblive` and fails with the install output when the template is not there.
- Verified in the Linux container (`mcr.microsoft.com/dotnet/sdk:10.0`): `package-tests.cs` passes with the fix and fails at the install check without it; see Notes.
