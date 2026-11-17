---
type: chore
priority: medium
created: 2026-11-17
---
# The packages ship without XML documentation

## Problem
No project sets `GenerateDocumentationFile`, so the packages have no `.xml` file next to their DLLs, and none of the `<summary>` comments of the public API reach IntelliSense. A user who hovers `DeployParameters.CreateDbIfNotExists`, `IDbLiveTester.RunAllTestsAsync` or `DeploymentTest.DeployAsync` sees nothing, and these comments say what is safe in production.

Without the documentation file the compiler does not check `cref` values either, so a wrong one stays unseen. One is known: `IDbLiveTester.cs:12` refers to `SelfDeployer.DatabaseRequiresNewerDbLiveException`, and the type is in the namespace `DbLive` (its file is in the folder `SelfDeployer`). From the code, not built.

Found by the code review of 2026-11-17 (finding 8).

## Where
- `src/Directory.Build.props` (`TreatWarningsAsErrors` on line 25; no `GenerateDocumentationFile`)
- The packed libraries: `DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`
- `src/DbLive/Testing/IDbLiveTester.cs:12` (the wrong `cref`)

## Fix
Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for the packed libraries, and `NoWarn` for CS1591 (a public member without a comment): warnings are errors, and not every public member has a comment. Fix the `cref` errors that the build then reports.

- [ ] In `Directory.Build.props` for all projects, tests and demos included, or only in the packed projects?
- [ ] With the documentation file the rule IDE0005 (unused `using`) can also run in the build (from memory, not verified). Fix what it reports, or keep it off?

## Done when
- [ ] Each `.nupkg` has the `.xml` file next to its DLL (check the output of `dotnet pack`)
- [ ] The build passes with warnings as errors; wrong `cref` values fixed
- [ ] IntelliSense shows the summary of a public member in a project that references the local package
