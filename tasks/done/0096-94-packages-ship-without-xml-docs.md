---
type: chore
priority: medium
created: 2026-11-17
closed: 2026-11-22
---
# The packages ship without XML documentation

## Release note
The packages now carry the XML documentation of their public API, so IntelliSense shows what a member does: `DbLiveBuilder` and its methods, `IDbLive.DeployAsync` with the exceptions it throws, the flags and presets of `DeployParameters`, `IDbLiveTester`, the fixture builders, `[SqlFact]` and the exceptions with their properties. Nothing to change on upgrade.

## Problem
No project sets `GenerateDocumentationFile`, so the packages have no `.xml` file next to their DLLs, and none of the `<summary>` comments of the public API reach IntelliSense. A user who hovers `DeployParameters.CreateDbIfNotExists`, `IDbLiveTester.RunAllTestsAsync` or `DeploymentTest.DeployAsync` sees nothing, and these comments say what is safe in production.

Without the documentation file the compiler does not check `cref` values either, so a wrong one stays unseen. One is known: `IDbLiveTester.cs:12` refers to `SelfDeployer.DatabaseRequiresNewerDbLiveException`, and the type is in the namespace `DbLive` (its file is in the folder `SelfDeployer`). Verified by a build with the documentation file: CS1574.

Found by the code review of 2026-11-17 (finding 8).

## Where
- `src/Directory.Build.props` (`TreatWarningsAsErrors` on line 25; no `GenerateDocumentationFile`)
- The packed libraries: `DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`
- `src/DbLive/Testing/IDbLiveTester.cs:12` (the wrong `cref`)

## Fix
Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for the packed libraries, and `NoWarn` for CS1591 (a public member without a comment): warnings are errors, and not every public member has a comment. Fix the `cref` errors that the build then reports.

- [x] In `Directory.Build.props` for all projects, tests and demos included, or only in the packed projects? Maintainer, 2026-11-22: only the six packed projects, and without `NoWarn` for CS1591: every public member gets a doc comment, and CS1591 stays an error, so a new public member without one does not build.
- [x] With the documentation file the rule IDE0005 (unused `using`) can also run in the build (from memory, not verified). Fix what it reports, or keep it off? Nothing to fix: the documentation file alone does not make the build report IDE0005, because its default severity is hidden and `.editorconfig` does not raise it (verified: no IDE0005 in the build of the solution). Raising it to an error would be a task of its own.

## Docs to update
- `Development.md` ("The public API": every public member has a doc comment, and the build checks them)
- `docs/llms-full.txt`: no change, DbLive behaves as before

## Done when
- [x] Each `.nupkg` has the `.xml` file next to its DLL (check the output of `dotnet pack`)
- [x] The build passes with warnings as errors; wrong `cref` values fixed
- [x] IntelliSense shows the summary of a public member in a project that references the local package

## Notes
- Verified, a build of the six packages with `-p:GenerateDocumentationFile=true` before the change: 85 public members without a comment (CS1591: 60 in `DbLive`, 7 each in `DbLive.MSSQL`, `DbLive.PostgreSQL` and `DbLive.xunit`, 2 each in the Testcontainers packages), among them the main entry points: `DbLiveBuilder`, `IDbLive.DeployAsync`, the flags and presets of `DeployParameters`, `TestRunResult`. And 13 errors in comments that existed: the wrong `cref` above (CS1574); `<param name="settings">` of a parameter that is gone in `IUnitTestItemRunner` (CS1572); `<param>` tags for only some of the parameters (CS1573) in `IDbLiveDA` (3 methods), `NotSupportedTransactionIsolationLevelException`, `TranIsolationLevelMapper.ValidateForProvider` and `SqlFactDiscoverer.GetSourceScriptsPath`. The compiler checks the comments of internal members too.
- `GenerateDocumentationFile` is set in each of the six `.csproj`, next to `PublicApiAnalyzers`, which is scoped to the same projects; not in `Directory.Build.props`, which is imported before the body of the `.csproj` and cannot tell a packed project from a test project. Tests and demos are not checked.
- The comments follow `docs/llms-full.txt` (Sections 11, 12 and 17) and the code. Interface implementations take `<inheritdoc/>`. `IDbLive.DeployAsync` lists the exceptions of the deployment itself; the errors of the project files are named in its summary as a group.
- Verified: `CreateDeployer()` and `CreateTester()` of a builder without a provider, a connection or a project throw `InvalidOperationException` (the container cannot resolve a service), as their comments say.
- `IDbLiveTester.RunAllTestsAsync` now also names `DatabaseNotFoundException` and `NotSupportedTransactionIsolationLevelException`, which it throws (Section 8.2).
- The `.xml` file holds the comments of internal members as well; IntelliSense shows only the public ones. The source is public, so nothing is exposed.
- `package-tests.cs` checks the packages: every DLL under `lib/` has its `.xml` next to it, for the same assembly, with documented members. Verified both ways: it passes on the packages of this build, and fails on a copy of `DbLive.nupkg` without `lib/net9.0/DbLive.xml`.
- Verified: a project that references the local packages (`DbLive.xunit` and both Testcontainers packages, which bring the other three) gets `lib/net10.0/<assembly>.xml` next to each DLL in its NuGet cache, the file IntelliSense reads; `DbLive.xml` holds the summary of `DeployParameters.CreateDbIfNotExists`. Not looked at in an IDE.

## Result
- The six packed projects set `GenerateDocumentationFile`; CS1591 is not suppressed, so a public member without a doc comment fails the build.
- All 85 public members got doc comments, written for the user at the call: `DbLiveBuilder` (with an example), its extension methods, `IDbLive.DeployAsync` (with its exceptions), `DeployParameters`, `IDbLiveTester`, `TestRunResult`, the provider extensions, the connection string helpers, the fixture builders, `DbLiveTestFixture`, `SqlFactAttribute.TestFixture`, and the exceptions with their properties.
- The 13 errors in existing comments are fixed, the wrong `cref` among them.
- `package-tests.cs` checks that every package carries the `.xml` next to its DLL (`package-tests/Packages.cs`).
- `Development.md` ("The public API") says that every public member has a doc comment and that the build checks them.
- Tests: the solution builds in Release with warnings as errors; `DbLive.Tests` pass; the package tests pass.
