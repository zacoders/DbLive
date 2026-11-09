---
type: bug
priority: high
created: 2026-11-09
---
# The self-deployment changes the `dblive` tables before the project is checked

## Problem
`DbLive.DeployAsync` runs the self-deployment (the self-migrations of the `dblive` tables) before it reads the project. The checks of the project run later, inside `DbLiveDeployer`:

- the read of the files: `FilesOutsideProjectFoldersException`, `ScriptPathsDifferOnlyByCaseException`, `BreakingScriptMatchesTestPatternException`;
- the names of migration files: `UndoNotSupportedException`, `ObsoleteMigrationSettingsFileException`, `InvalidMigrationVersionException`;
- the settings files of migration items: `InvalidMigrationItemSettingsException`;
- the transaction settings of `settings.json`: `TransactionSettingsValidator`.

So a deployment that fails on an error of the project has already changed the `dblive` tables.

It matters most on the first deployment after the upgrade to 2.0. The self-migrations added since 1.5.0 (07 to 13 on SQL Server, 04 to 09 on PostgreSQL) cannot be undone: they delete the undo rows, drop `dblive.migration.content`, rename the `settings` rows to `migration.settings`, and write `min_dblive_lib_version = 2.0.0`. The errors above are the ones an upgrade meets: an undo script left in the project, an old `<version>.settings.json`, the key `MigrationTimeout`, a `README.md` in `Scripts`.

Scenario (from the code, not verified by a test): the packages of a branch are updated to 2.0, and the project still has `Migrations/001.undo.sql`. The deployment to a shared dev or staging database fails with `UndoNotSupportedException`. Nothing of the project is deployed, but the database is now in the format of 2.0. The pipeline of `main`, still on 1.5, fails on every deployment from now on: 1.5 writes the column `content` (`Invalid column name 'content'`) and cannot read `migration.settings` or `removed`. The docs say not to deploy a database that DbLive 2.0 has deployed with an older version (§18.8, the release note of 0096-40); nobody expects a failed deployment to count.

The same holds for every later release that raises `MinCompatibleVersion`: a deployment that fails on its project raises the version anyway, and the previous version stops with `DatabaseRequiresNewerDbLiveException`.

The comment in `DbLiveDeployer` "an older build fails here, before anything is changed" is wrong for the same reason: the self-deployment has already run.

The ProjectId check moved before the self-deployment for the same reason (0096-52).

Found by the code review of 2026-11-09 (finding 1).

## Where
- `src/DbLive/DbLive.cs:36-46` (`DeployAsync`: `ProjectIdValidator.ValidateAsync`, `_selfDeployer.DeployAsync`, then `_deployer.DeployAsync`)
- `src/DbLive/Deployers/DbLiveDeployer.cs:24-34` (`TransactionSettingsValidator`, `AppliedMigrationsValidator`, the comment)
- `src/DbLive/Project/DbLiveProject.cs:124` (`GetMigrationsAsync`), `:184` (settings files of items), `:316` (`ReadFiles`, its comment "the deployment stops at its first read")
- The phases that read the project through `IDbLiveProject`: `AppliedMigrationsValidator`, `MigrationChecksumValidator`, `MigrationsSaver`, `MigrationsDeployer`, `CodeDeployer`, `CodeBreakingDeployer`, `BreakingChangesDeployer`, `FolderDeployer`

## Fix
Split the deployment in two stages: load the project, then deploy. A loader reads and checks the whole project before anything touches the server and returns an immutable snapshot. The phases of the deployment take the snapshot as an argument and no longer depend on `IDbLiveProject`, so nothing reads the project after the self-deployment, and a new read cannot fall behind it again.

### Snapshot
`ProjectSnapshot` in `src/DbLive/Project/`, data without behavior:

```csharp
internal sealed record ProjectSnapshot
{
	public required IReadOnlyList<Migration> Migrations { get; init; }
	public required IReadOnlyList<CodeGroup> CodeGroups { get; init; }
	public required IReadOnlyList<GenericItem> CodeBreakingItems { get; init; }
	public required IReadOnlyList<GenericItem> BeforeDeployItems { get; init; }
	public required IReadOnlyList<GenericItem> AfterDeployItems { get; init; }
}
```

The tests (`GetTestsAsync`) are not in it: the deployment does not run them. `DbLiveTester` and `UnitTestsRunner` keep `IDbLiveProject`.

### Loader
`ProjectLoader : IProjectLoader` (internal), `Task<ProjectSnapshot> LoadAsync()`:

1. `settings.json` and `TransactionSettingsValidator`, which needs only `_da.Provider` and no connection.
2. `GetMigrationsAsync()`: the checks of `ReadFiles` (all files of the project), the names of migration files, the settings files of items.
3. `GetCodeGroupsAsync()`, `GetCodeBreakingItemsAsync()`, `GetFolderItemsAsync(BeforeDeploy)`, `GetFolderItemsAsync(AfterDeploy)`: every file the deployment runs is read here, so a file that cannot be read fails here too.

It loads the whole project whatever the `DeployParameters`, so `LoadAsync` takes no parameters.

It reads through `IDbLiveProject`, which keeps its cache (0096-44): the xUnit fixture and `DbLiveTester` of the same builder do not read the files again. 0096-72 adds its check to the loader.

### `DbLive.DeployAsync`
The load is the first step, before `RecreateDatabase` and `CreateDbIfNotExists`: an error of the project leaves the server as it was, a database that does not exist yet included.

```
LoadAsync
→ RecreateDatabase / CreateDbIfNotExists / CheckDatabaseExists
→ ProjectId check → self-deployment → ProjectId bind
→ _deployer.DeployAsync(snapshot, parameters)
```

### Phases
`IDbLiveDeployer.DeployAsync(ProjectSnapshot, DeployParameters)` passes the snapshot to every phase; each phase drops `IDbLiveProject` and uses the parts it needs:

| Phase | Reads now | Uses |
|---|---|---|
| `AppliedMigrationsValidator` | `GetMigrationsAsync` | `Migrations` |
| `MigrationChecksumValidator` | `GetMigrationsAsync` | `Migrations` |
| `MigrationsSaver` | `GetMigrationsAsync` | `Migrations` |
| `MigrationsDeployer` | `GetMigrationsAsync` | `Migrations` |
| `FolderDeployer` | `GetFolderItemsAsync` | `BeforeDeployItems`, `AfterDeployItems` |
| `CodeDeployer` | `GetCodeGroupsAsync`, `GetCodeBreakingItemsAsync` | `CodeGroups`, `CodeBreakingItems` |
| `BreakingChangesDeployer` | `GetCodeGroupsAsync`, `GetMigrationsAsync` | `CodeGroups`, `Migrations` |
| `CodeBreakingDeployer` | `GetCodeBreakingItemsAsync` | `CodeBreakingItems` |

The phases keep `ISettingsAccessor`: `settings.json` is read and checked before the self-deployment already (the ProjectId check reads it).

### Visibility
The types whose signatures change become internal: `IDbLiveDeployer` and `DbLiveDeployer`, the eight phases above and their interfaces, `ProjectSnapshot`, `IProjectLoader`, `ProjectLoader`. `IDbLiveProject` stays public: `DbLiveBuilder.CreateProject()` returns it. `DbLive.csproj` gets `<InternalsVisibleTo Include="DynamicProxyGenAssembly2" />`, so NSubstitute can mock the internal interfaces (the assembly is not signed, no public key).

`MockSet` in `DbLive.Tests.Common` has public fields of the phase interfaces, and `DbLive` does not show its internals to that project. Only `DbLive.Tests` uses `MockSet` (verified with grep; `DbLive.MSSQL.Tests` and `DbLive.PostgreSQL.Tests` use other helpers of the project), so it moves to `DbLive.Tests`.

The other public types behind the dependency injection are 0096-73.

### Tests
- The unit tests of the phases build a `ProjectSnapshot` instead of mocking `IDbLiveProject` (8 files). A helper in `DbLive.Tests.Common` builds a snapshot with empty lists, the test sets the parts it needs.
- `ProjectLoader`: every read is made, and an exception of the project comes out of `LoadAsync`.
- `DbLive`: an exception of the loader, and nothing is called on `IDbLiveDA`, `IProjectIdValidator` or `IDbLiveSelfDeployer`.

### Open questions
- [x] Load everything always, or only what the `DeployParameters` deploy? Both standard runs read everything already: `Default` reads migrations, code, the breaking scripts of the Code folder (`CodeDeployer.MarkRemovedFilesAsync`) and both folders; `Breaking` reads migrations, code (`ValidateCodeDeployedAsync`), the breaking scripts and both folders. Only a custom run with `DeployCode = false` and `DeployBreaking = false` reads less: loading everything makes it fail on a code file that cannot be read. Decided 2026-11-09: load everything.
- [x] The phase interfaces (`IDbLiveDeployer`, `ICodeDeployer`, `IMigrationsSaver`, ...) and most phase classes are public, and `ConfigureServices` can replace them, though no doc names them as an extension point. Their signatures change. Make them internal in the same change, so `ProjectSnapshot` can be internal too? Decided 2026-11-09: internal, see Visibility and Notes. Not `breaking: true`: `docs/versioning.md` says that public types not described in the docs, the deployers among them, may change in any version.

## Docs to update
- `docs/llms-full.txt` §12 (order of `DeployAsync`: the project is read and checked first, step 7 is no longer the first read), §18.8 (a deployment that fails on its project changes nothing on the server)
- `docs/deployment/index.md` (the list of steps, "The DbLive version a database requires")
- `docs/versioning.md` (the last paragraph names "the deployers" among the public types not described in the docs: they are internal now)

## Done when
- [ ] `ProjectLoader` and `ProjectSnapshot`; no phase of the deployment depends on `IDbLiveProject`
- [ ] The phases, `DbLiveDeployer`, the loader and the snapshot are internal
- [ ] Unit test: an error of the project fails `DeployAsync` before the database is created and before the self-deployment
- [ ] Integration test on SQL Server and PostgreSQL: a new database and a project with `Migrations/001.undo.sql`: the deployment fails with `UndoNotSupportedException` and the database has no `dblive` schema; with `CreateDbIfNotExists` and no database, the database is not created
- [ ] The comments in `DbLiveDeployer` and `DbLiveProject.ReadFiles` corrected
- [ ] Docs updated

## Notes
- 2026-11-09, maintainer: the snapshot (two stages, the phases take the loaded project) over a preflight step. The preflight step: `DbLive.DeployAsync` calls the same reads of `IDbLiveProject` before the self-deployment, and the phases stay as they are and take the data from the cache. Smaller, but it holds only by convention: a new read in a phase that the preflight step does not make runs after the self-deployment again, and no test notices.
- 2026-11-09, maintainer: the phases become internal. A phase is not an extension point: a replaced `ICodeDeployer` writes the rows of `dblive.code` that the breaking guard and later DbLive versions rely on. They were public because a public class cannot take an internal type in its constructor, not by design; the provider packages and `DbLive.xunit` see the internals through `InternalsVisibleTo`, and outside `src/DbLive` only `MockSet` of the tests uses the phases (verified with grep). Plugins, when a real case comes, get a narrow documented point (an observer of the deployment), not the replacement of a phase.
- Verified in the code: `TransactionSettingsValidator` uses only `IDbLiveDA.Provider`, a constant of the provider class, so the load can run before the database exists.
- The integration test uses a new database: on a database deployed by this DbLive no self-migration is pending, so "the version in `dblive.version` does not change" would pass without the fix.
- Not covered by the fix: `AppliedMigrationsValidator` and `MigrationChecksumValidator` compare the project with the `dblive` tables in the format of this DbLive, so they stay after the self-deployment. So do the errors of the SQL of the project.
