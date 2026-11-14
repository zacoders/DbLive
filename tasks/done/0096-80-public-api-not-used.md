---
type: chore
priority: medium
created: 2026-11-14
closed: 2026-11-14
breaking: true
---
# Public API that a user does not use

## Release note
**`IDbLiveDA` only drops the database; the rest of DbLive's database access is internal.**

To upgrade:

- `IDbLiveDA` keeps `DropDbAsync`. `Provider`, `CreateDBAsync`, `ExecuteNonQueryAsync` and `ExecuteQueryMultipleAsync` are internal, and so are `SqlResult`, `SqlRow`, `SqlColumn`, `DbProvider` and `TranIsolationLevel`. To create the database, deploy with `CreateDbIfNotExists = true`. To run other SQL, also to query the `dblive` tables, use a connection of your own (ADO.NET, Dapper) with the same connection string.
- `TestRunResult.Output` and `ExecutionTimeMs` are internal: `RunTestAsync` writes the output of the test to its `writeLine`, and the test framework measures the time.
- `InvalidMigrationVersionException.MigrationVersionStr` is removed: the message has the reason.
- `DeploymentTestFixture` is sealed: create it with `new`, as the template does.

## Problem
After 0096-73, `DbLive` has 46 public types and 186 lines of public API. A second pass over every line of the `PublicAPI.Shipped.txt` files (2026-11-14) compared them with what a user calls or reads: the template, the demos, the docs, the READMEs of the packages (verified with grep). Some public API is used only by DbLive's own packages and tests, and they see the internals:

- `IDbLiveDA` has 7 members, and a user calls one: `DropDbAsync`, in the recipe for tests without xUnit (`docs/unit-testing/index.md`, Section 8.2 of `docs/llms-full.txt`). The others:
  - `CreateDBAsync` and `CheckDatabaseExistsAsync`: `DeployAsync` creates the database (`CreateDbIfNotExists`) and checks it, `RunAllTestsAsync` checks it.
  - `Provider`: the user sets the provider on the builder.
  - `ExecuteNonQueryAsync` and `ExecuteQueryMultipleAsync`, "raw database access": a user who has a builder has its connection string and their own ADO.NET or Dapper, and a `[SqlFact]` or deployment test cannot reach the builder of its fixture. They keep `SqlResult`, `SqlRow`, `SqlColumn` and `TranIsolationLevel` public, an API that would be frozen until 3.0.0: lists the caller can change, `SqlRow : List<object>` with `Add` and `Clear`, `GetValue<T>` that returns `default` for an unknown column.
  - `GetSchemaSnapshotAsync`: the schema check of `DeploymentTestFixture` (`DbLive.xunit`, which sees the internals).
- `DbLive.Schema`: `SchemaSnapshot`, `SchemaObject`, `SchemaObjectType`, `SchemaDiff`, `SchemaObjectChange` are used by the schema check of `DeploymentTestFixture`, by the providers and by tests. A user sees `SchemaChangedException` as a failed deployment test, with the differences in its message; `SchemaChangedException.Diff` is read only by tests.
- `TestRunResult.Output` and `ExecutionTimeMs`: `RunTestAsync` already writes `Output` to the `writeLine` it gets, and `Output` is `"Success"` or the error that `ErrorMessage` holds; xUnit measures the time of a test. The template and the demos read `IsSuccess` and `ErrorMessage`.
- `InvalidMigrationVersionException.MigrationVersionStr` holds the reason, which the message has; its name says otherwise. Nothing reads it.
- `DeploymentTestFixture` is not sealed. The template and the demos create it with `new`, none derives from it; 0096-73: a public class that the user does not derive from is sealed.
- `IScriptSource` and `SetScriptSource`: scripts from a source of your own. 0096-73 added `SetScriptSource` to keep a use of `ConfigureServices` that the docs named. No scenario needs it: `SetProject` reads the assembly, `SetScriptsFolder` a folder, and only tests call `SetScriptSource`. `IScriptSource` is an interface the user implements, so a member added later breaks every implementation.

The release is 2.0.0, a major version with breaking changes already. After it, each of these changes needs 3.0.0.

## Where
- `src/DbLive/Adapter/IDbLiveDA.cs`, `IDbLiveInternalDA.cs`, `SqlResult.cs`, `SqlRow.cs`, `SqlColumn.cs`; `src/DbLive/Common/DbProvider.cs`, `TranIsolationLevel.cs`
- `src/DbLive/Schema/`
- `src/DbLive/Deployers/Testing/TestRunResult.cs`, `src/DbLive/Project/Exceptions/InvalidMigrationVersionException.cs`
- `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs`
- The tests of the providers, which call these members through `CreateDbLiveDA()`
- `PublicAPI.Shipped.txt` of `DbLive` and `DbLive.xunit`

## Fix
- `IDbLiveDA` keeps `DropDbAsync`; the other six members move to `IDbLiveInternalDA`. `SqlResult`, `SqlRow`, `SqlColumn`, `DbProvider` and `TranIsolationLevel` are internal.
- The types of `DbLive.Schema` are internal, but `SchemaChangedException`, which moves to `DbLive.Testing`: the deployment test is a test, and `DbLive.Schema` has no public type left. Its `Diff` is internal.
- `TestRunResult.Output` and `ExecutionTimeMs` are internal.
- `InvalidMigrationVersionException.MigrationVersionStr` is deleted.
- `DeploymentTestFixture` is sealed.
- `IScriptSource` is internal, and `SetScriptSource` is deleted: nothing in DbLive calls it, and the tests register a source with the internal `ConfigureServices`.

Checked and kept:

- The 27 exceptions of Section 17. Most are errors in the project that no code catches, but the docs, the logs and agents name them by type, and an internal exception can be caught only as `Exception` (CA1064).
- `LogTo(Serilog.ILogger)`: a deployer that runs in an application or a service sends DbLive's log to its logger (a file, Seq); without it, the log goes only to the console or the xUnit output. Its cost: Serilog's `ILogger` is in the public API, so a move to `Microsoft.Extensions.Logging` would be a breaking change. Kept, decided 2026-11-14.
- The connection string helpers (`SetMsSqlDatabaseName`, `SetRandomMsSqlDatabaseName` and those of PostgreSQL): a custom fixture builder needs a new database name, and the docs point to `MsSqlFixtureBuilder` as the example, which calls `SetRandomMsSqlDatabaseName`.
- `GetBuilderAsync` and `GetProjectAssembly` of the fixture builders: the recipe for tests without xUnit calls `GetBuilderAsync`.
- `DbLiveTestFixture.InitializeAsync` and `DisposeAsync`: see the Notes of 0096-73.
- `TestRunResult.Exception`: the error with its inner exceptions (the provider's error); `ErrorMessage` has only its message.
- `CodeNotDeployedException.Count` and `FilesOutsideProjectFoldersException.Count`: `RelativePaths` holds only the first files.
- `DbLiveSqlException` and its `SqlError`: the inner exception of `MigrationDeploymentException` and `FolderDeploymentException`.

## Docs to update
- `docs/llms-full.txt` §11 (namespaces; 11.1 the comment of `CreateDbLiveDA`, `SetScriptSource` and `IScriptSource`; 11.4 `IDbLiveDA`; 11.5 `TestRunResult`)
- `docs/versioning.md` (`IScriptSource`, the types the members return)
- `Development.md` ("The public API": the namespaces, the interfaces users implement)
- The release note of `tasks/done/0096-73-public-types-outside-the-api.md` (same release): it says `IDbLiveDA` keeps the members that run SQL and read the schema, recommends `ExecuteQueryMultipleAsync` for the `dblive` tables, and offers `SetScriptSource`
- The release note of `tasks/done/0096-31-01-script-source.md` (same release): scripts from another place come from your own `IScriptSource`

## Done when
- [x] `IDbLiveDA` has only `DropDbAsync`; the rest is in `IDbLiveInternalDA`, and its types are internal
- [x] `DbLive.Schema` has no public type; `SchemaChangedException` is in `DbLive.Testing`, without a public `Diff`
- [x] `TestRunResult.Output`, `ExecutionTimeMs` and `InvalidMigrationVersionException.MigrationVersionStr` are not public; `DeploymentTestFixture` is sealed
- [x] `IScriptSource` is internal, `SetScriptSource` is deleted
- [x] `PublicAPI.Shipped.txt` updated; the solution builds, the tests pass
- [x] Docs updated

## Notes
- What 1.5.0 had, for the release note: `IDbLiveDA.Provider`, `CreateDBAsync`, `DropDbAsync`, `ExecuteNonQueryAsync`, `ExecuteQueryMultipleAsync`, `SqlResult`, `SqlRow`, `SqlColumn`, `DbProvider`, `TranIsolationLevel`, `TestRunResult.Output` and `ExecutionTimeMs`, `InvalidMigrationVersionException.MigrationVersionStr` (verified with `git show 1.5.0:...`). `CheckDatabaseExistsAsync`, `GetSchemaSnapshotAsync`, the types of `DbLive.Schema` and `SchemaChangedException` came in this release and were never released, so the release note does not name them. `DeploymentTestFixture` is `DeployFixture` renamed in this release (0096-32-02).
- The types that became internal keep their namespaces (`DbLive`, `DbLive.Schema`) and the explicit internal constructors of 0096-73: no code outside DbLive's packages and tests sees them, and the change stays small.
- `DeploymentTestFixture` gets the schema snapshot from `IDbLiveInternalDA` (`builder.GetRequiredService<IDbLiveInternalDA>()`, internal); it drops the database through `CreateDbLiveDA()`, as `DbLiveTestFixture` does.
- Tests. The provider tests call the internal interface everywhere: `CreateDbLiveDA()` became `GetRequiredService<IDbLiveInternalDA>()`, `IDbLiveDA` became `IDbLiveInternalDA` (24 files). `FixtureBuilderTests` checks that `CreateDbLiveDA()` returns the same instance as the internal interface, for both providers. `DeploymentTestFixtureTests` mocks `IDbLiveInternalDA` and registers it as both interfaces. The theories of `TranIsolationLevelMapperTests` take the name of the level (`nameof(TranIsolationLevel.Snapshot)`), as the theories of 0096-73 do: a public test method cannot take an internal type.
- The PublicApiAnalyzers do not track `sealed` on a type: sealing `DeploymentTestFixture` changed no line of `PublicAPI.Shipped.txt` of `DbLive.xunit` (verified with a build).
- The release note of 0096-73 (same release) said that `IDbLiveDA` keeps the members that create, check and drop the database, run SQL and read the schema, recommended `ExecuteQueryMultipleAsync` for the `dblive` tables, and listed `SqlResult`, `SqlRow`, `SqlColumn`, `DbProvider`, `TranIsolationLevel`, `SchemaSnapshot` and `SchemaObject` among the public types. It is updated, with a line in its Notes.
- 2026-11-14, maintainer, on the first version of this task, which kept `IScriptSource`, `SetScriptSource` and `LogTo` as "decided in 0096-73": close `IScriptSource` and `SetScriptSource`, keep `LogTo`. 0096-73 records the maintainer's decision that `ConfigureServices` is not public; the two methods that replaced its documented uses were not decided on their own.
- `IScriptSource` and `SetScriptSource` came in this release (0096-31-01 and 0096-73) and were never released: the release note of this task does not name them. The release notes of 0096-31-01 and 0096-73 offered them; both are updated, with a line in their Notes. A 1.5.0 user who read the scripts from another place through `IProjectPath` or `IFileSystem` has no replacement now; the release note of 0096-31-01 says so.
- `IScriptSource` keeps its namespace `DbLive`, and `IInternalScriptSource` (DbLive's own scripts in the providers) still derives from it. The two tests of `SetScriptSource` in `DbLiveBuilderExtensionsTests` are deleted; `DbLiveBuilderTests` already reads a project from an `InMemoryScriptSource` registered with `ConfigureServices`.

## Result
- `IDbLiveDA` has one member, `DropDbAsync`; `Provider`, `CreateDBAsync`, `CheckDatabaseExistsAsync`, `ExecuteNonQueryAsync`, `ExecuteQueryMultipleAsync` and `GetSchemaSnapshotAsync` moved to the internal `IDbLiveInternalDA`. Internal now: `SqlResult`, `SqlRow`, `SqlColumn`, `DbProvider`, `TranIsolationLevel`, `SchemaSnapshot`, `SchemaObject`, `SchemaObjectType`, `SchemaDiff`, `SchemaObjectChange`, `SchemaChangedException.Diff`, `TestRunResult.Output` and `ExecutionTimeMs`. `SchemaChangedException` is in `DbLive.Testing`. `InvalidMigrationVersionException.MigrationVersionStr` is deleted. `DeploymentTestFixture` is sealed. `IScriptSource` is internal, `SetScriptSource` is deleted.
- `DbLive` has 35 public types (46 before) and 97 lines of public API (186 before), in two namespaces, `DbLive` and `DbLive.Testing`. The other packages did not change.
- Kept public after the check: see "Checked and kept" under Fix.
- Docs: `docs/llms-full.txt` §11, 11.1, 11.4, 11.5 and 13.3, `docs/versioning.md`, `Development.md` ("The public API"); the release notes of 0096-31-01 and 0096-73.
- Tests: `DbLive.Tests` 475, `DbLive.MSSQL.Tests` 77, `DbLive.PostgreSQL.Tests` 55, `DemoMSSQL.Tests` 12, `Demo.PostgreSQL.Chinook.Tests` 5, `AdventureWorks.Tests` 4 pass (`--filter "Category!=LocalOnly"`, as CI runs them); `demo/Demo.sln` builds.
