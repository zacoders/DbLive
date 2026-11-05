---
type: chore
priority: low
created: 2026-11-05
---
# Small fixes from the code review of 2026-11-05

## Problem
Small findings of the code review of #96, each a few lines. Larger ones have their own tasks: 0096-40 to 0096-44.

### 1. The log of removed code files shows the lower-case path
`CodeDeployer.MarkRemovedFilesAsync` logs "Code files removed from the project, marked as removed in dblive.code: ..." with the paths returned by the database, which are lower case (`code/dbo/getuser.sql`). `docs/llms-full.txt` §15 says: "Logs and exceptions show the real path of the file; only downgrade messages show the stored lower-case path". The file is gone from the project, so its real path is not known either.

- `src/DbLive/Deployers/Code/CodeDeployer.cs:54`
- [ ] Fix the docs: this log shows the stored path too, like the downgrade messages, because the files are gone from the project.

### 2. File name parts of migrations are compared with the current culture
`MigrationFileNameParser` lower-cases the type part (`migrationFilePart2.ToLower()`, line 18) and the settings part (`IsSettingsPart`, line 88) with `ToLower()`, which depends on the culture. The docs say the parts are compared ignoring case on every OS. In the `tr-TR` culture `I` becomes `ı`: `001.BREAKING.sql` is not a breaking script but a migration named `BREAKING` (`DuplicateMigrationItemException` next to `001.migration.sql`), and `001.MIGRATION.SETTINGS.json` fails with `UnknownMigrationSettingsException`.

- `src/DbLive/Project/MigrationFileNameParser.cs:18`, `:88`
- [ ] `ToLowerInvariant()` or `StringComparison.OrdinalIgnoreCase`, with a unit test under `tr-TR`. Also check the `ToString().ToLower()` of enums in `MsSqlDA` and `PostgreSqlDA` (`Status`, `ItemType` have no `I` in their names today, so they work, but invariant is safer).

### 3. `DeploymentTestFixture.Deployer` goes stale
The public property `Deployer` is set in `InitializeAsync`, but `DeployAsync` calls `LogToXUnitOutput`, which is `ConfigureServices`: it rebuilds the container and disposes the `ServiceProvider` that created `Deployer`. A test that calls `_fixture.Deployer!.DeployAsync(...)` after `_fixture.DeployAsync(output)` uses singletons of a disposed provider, and its logs do not reach the xUnit output. The fixture itself does not use the property; nothing in `src` or `docs` does. `DeployAsync` also adds one more logger registration to the shared builder on each call.

- `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs:17`, `:24`, `:49`
- [ ] Remove the property (a public API change: release note) or set it in `DeployAsync`?

### 4. The two container fixture builders are copies
`MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder` are the same line by line (the static dictionary of starts, the lock, `GetServerAsync`); only the builder and container types differ. A fix of the caching (a retry after a failed start, disposing the container) has to be made twice.

- `src/DbLive.Testcontainers.MSSQL/MsSqlContainerFixtureBuilder.cs`, `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs`
- [ ] A shared internal helper (in `DbLive` or `DbLive.xunit`, visible to both packages) that takes a factory `Func<string, Task<string>>`.

### 5. A SQL Server test leaves a database behind
`MissingDatabaseTests.Login_without_access_to_an_existing_database_gets_the_error_of_SQL_Server` creates the database `DbLive--<guid>` and a login, but `finally` drops only the login. In CI the container is deleted; on a persistent local server the databases pile up.

- `src/DbLive.MSSQL.Tests/MissingDatabaseTests.cs:59`, `:79`
- [ ] Drop the database in `finally` too.

### 6. Task 0096-18 is in `done/` without being closed
`tasks/done/0096-18-package-assembly-version-and-symbols.md` has no `closed:`, no `## Result`, and two unticked items under `## Done when` that can only be checked after a release ("NuGet Package Explorer shows Source Link ...", "The next release on nuget.org has a symbols package"). AGENTS.md: close only when everything under `Done when` is ticked.

- [ ] Fill `## Result` and `closed:`, and move the two checks after the release to the 2.0.0 release checklist (0096-19 has one), or move the task back to `tasks/` until the release? Back in `tasks/`, its release note is missing from the notes of 2.0.0.

## Docs to update
- `docs/llms-full.txt` §15 (item 1)
- `docs/llms-full.txt` §13, if the `Deployer` property is removed (item 3)

## Done when
- [ ] Items 1–6 done or moved to their own tasks
- [ ] Unit test for item 2

## Notes
Found in the code review of 2026-11-05 (#96). Another finding of that review, the docs site published from the branch `96-documentation` (`.github/workflows/docs.yml`), is already the last item of D6 in `0096-documentation.md`.
