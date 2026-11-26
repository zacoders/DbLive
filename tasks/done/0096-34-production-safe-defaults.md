---
type: feature
priority: high
created: 2026-11-02
closed: 2026-11-02
breaking: true
---
# Safe defaults for production: no database creation, a warning without ProjectId

## Release note
**A deployment no longer creates the database by default.** `CreateDbIfNotExists` is now `false`, both in `new DeployParameters()` and in `DeployParameters.Default`. A typo in the database name of a production connection string now fails the deployment instead of creating an empty database and deploying the whole schema into it. A deployment into a database that does not exist fails with `DatabaseNotFoundException`, which names the database and the server. To upgrade: where the deployer has to create the database (a local or test database), deploy with `DeployParameters.Default with { CreateDbIfNotExists = true }`, or set `CreateDbIfNotExists = true` in your `new DeployParameters { ... }`. The fixtures of `DbLive.xunit` create their databases as before.

**A deployment without `ProjectId` logs a warning.** Set `ProjectId` in `settings.json` to a name unique to the project, for example `"ProjectId": "billing-api-db"`: the first deployment binds the database to it, and a deployment of another project into that database then fails with `ProjectIdMismatchException`.

## Problem
Found while working on 0096-22 (production settings in one place).

- `DeployParameters.CreateDbIfNotExists` defaulted to `true`, also in `DeployParameters.Default`, the preset of the production deployer in the docs. A typo in the database name of the connection string created a new, empty database and deployed the whole schema into it, and the deployment reported success. `ProjectId` does not catch it: a new database has no ProjectId and is bound on its first deployment. The deployer's login also needed permission to create databases (SQL Server: a connection to `master`).
- `ProjectId` is optional, and without it nothing stops a deployment into the database of another project. Nothing told the user so.

## Decision
2026-11-02, maintainer: `CreateDbIfNotExists = false` by default. A deployment without `ProjectId` logs a warning: the setting is recommended and important, without it databases can be damaged badly.

## Where
- `src/DbLive/DeployParameters.cs`: the `Default` preset and the property default
- `src/DbLive/Deployers/ProjectIdValidator.cs`: the branch for a missing ProjectId

## Fix
- [x] `CreateDbIfNotExists` is `false` in the property default and in `DeployParameters.Default` (`Breaking` was `false` already).
- [x] `ProjectIdValidator.ValidateAsync()` logs a warning when the settings have no ProjectId (absent, empty or whitespace). `ValidateBeforeTestsAsync()` does not.
- [x] Integration tests that deployed `Default` into a database that did not exist yet create it themselves.

## Docs to update
- `docs/llms-full.txt` rules 17 and 18, §3.3, §9.2, §11.3, §12, §14, §17, §18.1, §18.10
- `docs/llms.txt` (key facts)
- `docs/deployment/index.md` (deployer, deployment steps, `DeployParameters` table, presets, "Production")
- `docs/features/project-id.md`, `docs/project/configuration.md` (`ProjectId` row)
- `README.md`, `docs/gettingstarted.md` (quick start: `settings.json` with `ProjectId`, comment on `CreateDbIfNotExists = true`)

## Done when
- [x] Both defaults changed, with unit tests
- [x] Integration tests of both providers pass
- [x] Docs updated

## Notes
- Verified on SQL Server (Docker): a deployment into a database that does not exist fails with `SqlException: Cannot open database "<name>" requested by the login. The login failed. Login failed for user 'sa'.`, after about 10 seconds of connection retries by SqlClient. The PostgreSQL message (`3D000: database "<name>" does not exist`) is the Npgsql format, not run here (verified in 0096-35).
- The provider's error was misleading on SQL Server ("The login failed"). 2026-11-03, maintainer: check that the database exists and give a clear error; done in 0096-35 (`DatabaseNotFoundException`), so the release note names that exception.
- The warning comes only from deployments. `RunAllTestsAsync()` runs against a database the project was deployed to, and that deployment has already warned.
- Set `CreateDbIfNotExists = true` explicitly before, unchanged: `DbLiveTestFixture`, `DeploymentTestFixture` (`DbLive.xunit`), the demo `Program.cs` files and `LocalDeployTests`, `DeployAndUndoTests`, the quick starts of `README.md` and `docs/gettingstarted.md`. The demo `settings.json` files already set `ProjectId`.
- Tests that relied on the old default: `CodeBreakingTests`, `ItemTransactionSettingsTests` (both providers) and `CrossPlatformDeployTests` (PostgreSQL) create their database in `InitializeAsync`, as `CrossPlatformDeployTests` of SQL Server already did; `SqlServerIntegrationTestBase` creates it in its constructor; the undo test helper of `SchemaCheckTests` (both providers) sets `CreateDbIfNotExists = true`, because it drops the database at the end.
- The maintainer changed the example of the warning to `"ProjectId": "billing-api-db"`.

## Result
- `DeployParameters.CreateDbIfNotExists` and `DeployParameters.Default` no longer create the database; the property has an XML comment with the reason.
- `ProjectIdValidator` logs `ProjectId is not set in settings.json, so nothing stops this project from being deployed to the database of another project. Set ProjectId in settings.json, for example "ProjectId": "billing-api-db".` on every deployment without a ProjectId.
- Unit tests: `DbLiveTests.Database_is_not_created_by_default` (property default and both presets); `ProjectIdValidatorTests`: the warning for a missing or blank ProjectId, no warning when it is set or before the tests.
- `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 487 passed, 0 failed (DbLive.Tests 386, DbLive.MSSQL.Tests 46, DbLive.PostgreSQL.Tests 34, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4), with SQL Server and PostgreSQL in Docker. The `LocalOnly` demo tests need a local server and were not run. `mkdocs build --strict` passes.
- Docs: listed under "Docs to update"; the new "Production" section of `docs/deployment/index.md` is 0096-22.
