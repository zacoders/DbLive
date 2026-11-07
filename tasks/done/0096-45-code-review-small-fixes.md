---
type: chore
priority: low
created: 2026-11-05
closed: 2026-11-07
breaking: true
---
# Small fixes from the code review of 2026-11-05

## Release note
`DeploymentTestFixture.Deployer` is removed. The deployer it held belonged to a service provider that `DeployAsync` disposes, and its logs never reached the xUnit output. To upgrade, call `DeployAsync`, or create a deployer from a `DbLiveBuilder` of your own (`.LogToXUnitOutput(output)` sends its logs to the test output).

## Problem
Small findings of the code review of #96, each a few lines. Larger ones have their own tasks: 0096-40 to 0096-44.

### 1. The log of removed code files shows the lower-case path
`CodeDeployer.MarkRemovedFilesAsync` logs "Code files removed from the project, marked as removed in dblive.code: ..." with the paths returned by the database, which are lower case (`code/dbo/getuser.sql`). `docs/llms-full.txt` §3.4 and §15 say that logs show the real path of the file. The file is gone from the project, so its real path is not known either.

- `src/DbLive/Deployers/Code/CodeDeployer.cs:94`
- [x] Fix the docs: this log shows the stored path, because the files are gone from the project.

### 2. File name parts of migrations are compared with the current culture
`MigrationFileNameParser` lower-cases the type part and the settings part (`IsSettingsPart`) with `ToLower()`, which depends on the culture. The docs say the parts are compared ignoring case on every OS. In the `tr-TR` culture `I` becomes `ı`: `001.BREAKING.sql` is not a breaking script but a migration named `BREAKING` (`DuplicateMigrationItemException` next to `001.migration.sql`), and `001.MIGRATION.SETTINGS.json` fails with `UnknownMigrationSettingsException`.

- `src/DbLive/Project/MigrationFileNameParser.cs:20`, `:25`, `:93`
- [x] `ToLowerInvariant()`, with a unit test under `tr-TR`, also for the `ToString().ToLower()` of enums in `MsSqlDA` and `PostgreSqlDA`: moved to 0096-50 and done there.

### 3. `DeploymentTestFixture.Deployer` goes stale
The public property `Deployer` is set in `InitializeAsync`, but `DeployAsync` calls `LogToXUnitOutput`, which is `ConfigureServices`: it rebuilds the container and disposes the `ServiceProvider` that created `Deployer`. A test that calls `_fixture.Deployer!.DeployAsync(...)` after `_fixture.DeployAsync(output)` uses singletons of a disposed provider, and its logs do not reach the xUnit output. The fixture itself does not use the property; nothing in `src` or `docs` does. `DeployAsync` also adds one more logger registration to the shared builder on each call.

- `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs:16`, `:23`, `:47`
- [x] Remove the property (a public API change: release note) or set it in `DeployAsync`? Remove it, together with the `CreateDeployer()` call in `InitializeAsync` (maintainer, 2026-11-07).

### 4. The two container fixture builders are copies
`MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder` are the same line by line (the static dictionary of starts, the lock, `GetServerAsync`); only the builder and container types differ. A fix of the caching (a retry after a failed start, disposing the container) has to be made twice.

- `src/DbLive.Testcontainers.MSSQL/MsSqlContainerFixtureBuilder.cs`, `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs`
- [x] A shared internal helper (in `DbLive` or `DbLive.xunit`, visible to both packages) that takes a factory `Func<string, Task<string>>`? Skipped (maintainer, 2026-11-07): about 20 lines, and no change of the caching is planned.

### 5. A SQL Server test leaves a database behind
`MissingDatabaseTests.Login_without_access_to_an_existing_database_gets_the_error_of_SQL_Server` creates the database `DbLive--<guid>` and a login, but `finally` drops only the login. In CI the container is deleted; on a persistent local server the databases pile up.

- `src/DbLive.MSSQL.Tests/MissingDatabaseTests.cs:59`, `:79`
- [x] Drop the database in `finally` too? Closed without a change (maintainer, 2026-11-07): there is no persistent server. `SqlServerIntegrationFixture` always starts a container and disposes it after the run (`WithReuse(true)` is commented out), and the other integration tests do not drop their random databases either.

### 6. Task 0096-18 is in `done/` without being closed
`tasks/done/0096-18-package-assembly-version-and-symbols.md` has no `closed:`, no `## Result`, and two unticked items under `## Done when` that can only be checked after a release ("NuGet Package Explorer shows Source Link ...", "The next release on nuget.org has a symbols package"). AGENTS.md: close only when everything under `Done when` is ticked.

- [x] Fill `## Result` and `closed:`, and move the two checks after the release to the 2.0.0 release checklist (0096-19 has one), or move the task back to `tasks/` until the release? A task of its own for the checks after the release (maintainer, 2026-11-07): 0096-49.

## Docs to update
- `docs/llms-full.txt` §3.4 and §15, `docs/deployment/code.md` (item 1)

## Done when
- [x] Items 1–6 done or moved to their own tasks
- [x] Unit test for item 2 (in 0096-50)

## Notes
Found in the code review of 2026-11-05 (#96). Another finding of that review, the docs site published from the branch `96-documentation` (`.github/workflows/docs.yml`), is already the last item of D6 in `0096-documentation.md`.

- Item 3: the logs of `Deployer` did not reach the xUnit output even before `DeployAsync`: it was created in `InitializeAsync`, before `LogToXUnitOutput` added the logger. The logger registrations that pile up cost nothing in practice: the fixture is a field of the test class, so each fixture runs `DeployAsync` once.
- Item 6: 0096-19 is in `done/` too, with two unticked items about the release of 2.0.0, so the "release checklist" was itself in a closed task. Moving 0096-18 back to `tasks/` would take its release note out of the notes of 2.0.0 (`release-notes.cs` takes the tasks added to `tasks/done/` since the previous tag) and put it into a later release, while the change ships in 2.0.0. The checks of both tasks moved to 0096-49.
- Item 2 is in 0096-50, a `bug` task, so that its note goes under "Fixes": a task's note goes under one heading, and this task is breaking because of item 3.

## Result
- Item 1: `docs/llms-full.txt` §3.4 and §15, and `docs/deployment/code.md`, say that the log of code files removed from the project shows the stored lower-case paths. The release note of 0096-31-02 said the same of the downgrade, which 2.0.0 removes: it now names this log.
- Item 2: 0096-50 (`ToLowerInvariant()`, a unit test under `tr-TR`).
- Item 3: `DeploymentTestFixture.Deployer` removed, and `InitializeAsync` no longer builds a service provider that `DeployAsync` disposes at once.
- Items 4 and 5: no change, decisions above.
- Item 6: 0096-18 has `closed:` and `## Result`; its two checks after the release and the two open items of 0096-19 are in 0096-49.
- Tests: DbLive.Tests 425, SQL Server 54, PostgreSQL 43, demo projects 21 (AdventureWorks, Chinook, DemoMSSQL; they use `DeploymentTestFixture`): all pass.
