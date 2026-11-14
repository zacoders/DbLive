---
type: chore
priority: low
created: 2026-11-09
closed: 2026-11-14
---
# Public types of `DbLive` that are not part of the API

## Release note
The public types that the documentation does not describe are internal now: the deployers of single scripts, the test runners, the classes behind the dependency injection, the helpers, the data access classes of the providers (`MsSqlDA`, `PostgreSqlDA`) and their DTOs. `IDbLiveDA` keeps the members the documentation lists: create, check and drop the database, run SQL, read the schema. Its members that read and write DbLive's own tables (`GetMigrationsAsync`, `GetCodeItemsAsync`, `SaveCodeItemAsync`, `GetProjectIdAsync` and the others) are internal. Nothing to change on upgrade for code that uses the documented API. Code that read the `dblive` tables through `IDbLiveDA` queries them with `ExecuteQueryMultipleAsync` instead.

## Problem
`DbLive` had 136 public types when this task was written, 120 after 0096-65 made the phases internal (counted in `PublicAPI.Shipped.txt`), 33 of them exceptions. Most of the others are named nowhere in `docs/llms-full.txt` and used by no other project of `src` but the tests (verified with grep):

- the item deployers: `CodeItemDeployer`, `MigrationVersionDeployer`, `MigrationItemDeployer`;
- the test runners: `UnitTestsRunner`, `UnitTestItemRunner`, `AssertInfo`, `AssertType`, `ValidationResult`;
- the classes behind the dependency injection: `ITransactionRunner`, `TransactionScopeManager`, `ISettingsAccessor`, `ITimeProvider`, `IStopWatch`, `IDbLiveSelfDeployer`, `IProjectIdValidator`, `IMigrationVersionValidator`, `ITransactionSettingsValidator`;
- helpers: `StringExtensions`, `ListExtensions`, `HashExtensions`, `ExceptionExtensions`;
- `IInternalDbLiveProject` and `InternalMigration`, internal by name.

More are not documented and are used by the provider packages or `DbLive.xunit` (the DTOs of `IDbLiveDA`, `IDbLiveDbConnection`, `IInternalScriptSource`, `Bootstrapper`). Those packages see the internals of `DbLive` through `InternalsVisibleTo`, so they do not need them public. The provider packages have the same kind: `MsSqlDA`, `PostgreSqlDA` and their `Bootstrapper`.

The types are public because a public class cannot take an internal type in its constructor: once a class of the graph is public, its dependencies must be public too. Not by design. The graph starts at `DbLiveTester` and `DbLiveProject`: public classes, not documented (their interfaces are), whose constructors take `ISettingsAccessor`, `IUnitTestsRunner`, `IProjectIdValidator` and the others. `docs/versioning.md` says that such types may change in any version, so they do not hold back a change. But they show in IntelliSense next to the API, `ConfigureServices` can replace them, and a user who relies on one breaks without warning.

0096-65 makes the phases of the deployment internal (decided 2026-11-09); this task does the rest.

## Where
- All public types of `src/DbLive/`; the same check for `src/DbLive.MSSQL/`, `src/DbLive.PostgreSQL/`, `src/DbLive.xunit/` and the Testcontainers packages
- `src/DbLive.Tests.Common/InMemoryScriptSource.cs`: implements `IInternalScriptSource`, and `DbLive` does not show its internals to `DbLive.Tests.Common`. Only `DbLive.Tests` uses it (verified with grep), so it moves there, as `MockSet` did in 0096-65.
- `docs/versioning.md` (the last paragraph)

## Fix
Make a public type internal unless it is:

- described in the docs: Section 11 of `docs/llms-full.txt`, the exceptions of Section 17, what `docs/versioning.md` covers;
- in the signature of such a type: the models `IDbLiveProject` returns (`Migration`, `CodeGroup`, `GenericItem`, `TestItem`, `FileData`), the types of the members of `IDbLiveDA` that Section 11.4 lists.

The compiler finds the second group: a public member with an internal type does not compile.

### `IDbLiveDA`
It stays public: `DbLiveBuilder.CreateDbLiveDA()` returns it, and Section 11.4 lists 7 of its members (`Provider`, `CreateDBAsync`, `CheckDatabaseExistsAsync`, `DropDbAsync`, `ExecuteNonQueryAsync`, `ExecuteQueryMultipleAsync`, `GetSchemaSnapshotAsync`). Its other members read and write the `dblive` tables (`GetMigrationsAsync`, `SaveCodeItemAsync`, `GetSelfMigrationVersionAsync`, `SetProjectIdAsync`, `IsTransactionUsableAsync`, `ExecuteCodeItemAsync`, ...), and their DTOs are public only because the interface is. A provider outside this repository is not supported (decided 2026-11-09), so these members move to an internal interface that `MsSqlDA` and `PostgreSqlDA` implement too, and the DTOs become internal. `IDbLiveDA` keeps the members of Section 11.4.

### Tracking of the public API
`Microsoft.CodeAnalysis.PublicApiAnalyzers` in the six packages with a C# API: `DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`, `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`. A reference with `PrivateAssets="all"`, so the analyzer does not flow to the users of the packages. Each project has `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, both starting with `#nullable enable`, so a change of a nullable annotation counts too. No code depends on the target framework (no `#if NET...` in `src`), so one pair of files serves net8.0, net9.0 and net10.0.

`TreatWarningsAsErrors` (`src/Directory.Build.props`) makes the diagnostics build errors: RS0016, a public symbol not in the files; RS0017, a symbol of the files that is gone. A change of the public API does not build until the files say it, and the files are in the pull request.

The rules for the two files:

- `Shipped`: the public API of the last major version. `Unshipped`: what changed since.
- An added symbol goes to `Unshipped` (the code fix of RS0016 in the IDE). Nothing moves in a minor or patch release: every merge to `main` is a release.
- A removed or changed symbol of `Shipped` gets a `*REMOVED*` line in `Unshipped`. It is a breaking change: a major version only, the title of the pull request starts with `BREAKING CHANGE:`.
- A major release moves `Unshipped` to `Shipped`, and the removed lines leave `Shipped`.
- 2.0.0 is not released yet, so until it is, a line of `Shipped` is deleted instead of marked `*REMOVED*`.

The analyzer comes first, with the public API as it is now in `Shipped`: the commit that makes the types internal then shows in `Shipped` what left the API.

RS0026 and RS0027 (overloads with optional parameters) may fire on the existing API (not checked): fix the overloads or turn the rules off in `.editorconfig`, and say which under Notes. They do not: see Notes.

### Open questions
- [x] Is a provider written outside this repository a supported case? Decided 2026-11-09: not at this stage. See `IDbLiveDA` above.
- [x] A test of the public API, so that a new public type shows in the review? Decided 2026-11-09: yes, with `PublicApiAnalyzers` instead of a test, see Tracking of the public API and Notes.

## Docs to update
- `docs/versioning.md` (the last paragraph: which public types the policy does not cover; a provider outside this repository is not supported)
- `docs/llms-full.txt` §11.4 (`IDbLiveDA`: all its members now, not "selected members")
- `Development.md` (the public API files: what to do on RS0016 and RS0017; "Releases": a major release moves `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`)

## Done when
- [x] `PublicApiAnalyzers` in the six packages, committed with the API as it was before this task in `PublicAPI.Shipped.txt`
- [x] Every public type is described in the docs or in the signature of a described one; what stayed public and why is under Notes
- [x] `IDbLiveDA` has only the members of Section 11.4; the members for the `dblive` tables and their DTOs are internal
- [x] Docs updated

## Notes
- 2026-11-09, maintainer: `PublicApiAnalyzers` instead of a snapshot test with `PublicApiGenerator`. The analyzer fails the build (in the IDE and in the build step of CI, before the tests), a test fails only where the tests run. It makes a removal an explicit `*REMOVED*` line, where a snapshot shows it as one deleted line of a diff. It tracks nullable annotations and needs no code of ours. What it costs: two files per package, and its one-line-per-member format reads a little worse in a diff than the declarations of `PublicApiGenerator`.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0, the latest on nuget.org; the SDK 10.0.402 has the compiler 5.9.0, which loads it. The package adds `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` of the project directory to the analyzer itself. The files were filled with `dotnet format analyzers <project> --diagnostics RS0016 --severity info` (the code fix of RS0016, into `Unshipped`), then moved to `Shipped`. `dotnet format` changed no `.cs` file. Lines: `DbLive` 736, `DbLive.MSSQL` 40, `DbLive.PostgreSQL` 40, `DbLive.xunit` 26, each Testcontainers package 5.
- RS0026 and RS0027 do not fire on the existing API, also with the whole API in `Unshipped` (verified with a build of `DbLive`): nothing to fix or turn off.
- What stayed public in `DbLive`, 56 types (from 120): 27 exceptions, all of Section 17, and 29 others. Described in Section 11: `DbLiveBuilder`, `DbLiveBuilderExtensions`, `IDbLive`, `DeployParameters`, `IDbLiveDA`, `IDbLiveTester`, `IDbLiveProject` (`CreateProject()` returns it), `IScriptSource`, `IDbLiveFixtureBuilder`, `SchemaSnapshot`, `SchemaObject`, `SchemaDiff`, `TestRunResult`. In their signatures: `DbProvider`, `TranIsolationLevel`, `SqlResult`, `SqlRow`, `SqlColumn` (`IDbLiveDA`); `SchemaObjectType`, `SchemaObjectChange` (the schema types); `Migration`, `MigrationItem`, `MigrationItemType`, `CodeGroup`, `CodeItem`, `GenericItem`, `TestItem`, `FileData`, `ProjectFolder` (`IDbLiveProject`). The other packages keep what Sections 11.1, 11.6 and 13 describe: the `DbLiveBuilderExtensions` of each package, the connection string helpers, the fixture builders, `DbLiveTestFixture`, `DeploymentTestFixture`, `SqlFactAttribute`, `SqlDeployFactAttribute`.
- Internal now: 60 types of `DbLive` (4 more were deleted, see below), `MsSqlDA`, `PostgreSqlDA`, the `Bootstrapper` of each provider, and `SqlFactDiscoverer` and `SqlXunitTestCase` of `DbLive.xunit`. xUnit creates the last two by reflection: by name from the `[XunitTestCaseDiscoverer]` of `SqlFactAttribute`, and on deserialization in Visual Studio, which serializes the test cases between discovery and run. Both have a public constructor, which is all reflection needs. Verified: `Discovered_test_case_survives_serialization` (new, `SqlFactDiscovererTests`) round-trips a discovered test case through xUnit's `SerializationHelper`, and the `[SqlFact]` tests of the three demo test projects run.
- Six exceptions are not in Section 17. `BadDeployParametersException`, `FileContentChangedException` and `ProjectFolderIsEmptyException` are thrown nowhere: deleted. `InvalidMigrationItemTypeException` and `UnknownMigrationItemTypeException` cannot reach the caller: only `*.sql` and `*.json` files reach `MigrationFileNameParser`, a `.json` file returns or throws before them, and a `.sql` file passes both checks. `DbLiveMigrationItemMissedSqlException` reaches the caller only as the inner exception of `MigrationDeploymentException` (`MigrationItemDeployer` wraps every error). The three are internal. `IDbLiveTransaction` was used nowhere: deleted.
- `IDbLiveDA` split: `internal interface IDbLiveInternalDA : IDbLiveDA` has the other members, named like `IInternalScriptSource` and `IInternalDbLiveProject`. Every internal class takes `IDbLiveInternalDA`, so a unit test mocks one interface. A provider registers its class once as `IDbLiveInternalDA` and resolves `IDbLiveDA` to the same instance.
- Tests. `MockSet.DbLiveDA` is an `IDbLiveInternalDA`; the fixture tests keep `IDbLiveDA`. A provider integration test that reads the `dblive` tables gets the internal interface with `builder.GetRequiredService<IDbLiveInternalDA>()`, an internal method of `DbLiveBuilder` that existed before. `DbLive.Tests` takes the provider assemblies from `MsSqlFixtureBuilder` and `PostgreSqlFixtureBuilder` instead of the DA classes. `DbLive.PostgreSQL` shows its internals to `DbLive.PostgreSQL.Tests`, which calls `InitializePostgreSQL` (`DbLive.MSSQL` already did so for its tests). A theory of `MigrationItemDeployerTests` takes a `bool`: a public test method cannot take the internal `TransactionWrapLevel`.
- `docs/versioning.md` now names `IDbLiveProject` and the types in the signatures of the listed ones (the project models, `SchemaSnapshot`, `TestRunResult`). They were public and reachable from a documented method before, but the list did not name them; with "every public type is in this list" a change of the project models is a breaking change now.
- Two release notes of tasks closed for the same release (2.0.0) said the opposite of this one; they are updated, with a line in their Notes: 0096-06 ("`InvalidMigrationItemTypeException` is now public too" left it) and 0096-19 (the types the documentation does not describe are internal, no longer "may change in any version").
- Public members of public types were not checked: the task is about types. One that looks internal: `FileData.MatchesHash(long)`, which knows the legacy hashes. The analyzer shows any change of it from now on.

## Result
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 in the six packages with a C# API; `PublicAPI.Shipped.txt` got the API as it was (first commit), and the second commit deletes from it what left the API: 393 lines of `DbLive`, 31 of each provider, 8 of `DbLive.xunit`. `Development.md` has the rules ("The public API") and a line in "Version" on the move of `Unshipped` to `Shipped`.
- Every public type is described in the docs or in the signature of a described one (list under Notes). 60 types of `DbLive`, the provider DA classes and `Bootstrapper`s, and the xUnit plumbing of `DbLive.xunit` are internal; 4 unused types are deleted.
- `IDbLiveDA` has the 7 members of Section 11.4; the others are in the internal `IDbLiveInternalDA : IDbLiveDA`, with their DTOs internal. One provider instance serves both interfaces.
- `InMemoryScriptSource` moved from `DbLive.Tests.Common` to `DbLive.Tests` and is internal.
- Docs: `docs/versioning.md` (what the policy covers; every public type is in that list; a provider outside the repository is not supported), `docs/llms-full.txt` §11.4.
- Tests: new `Discovered_test_case_survives_serialization`. `DbLive.Tests` 473, `DbLive.MSSQL.Tests` 77, `DbLive.PostgreSQL.Tests` 55, `DemoMSSQL.Tests` 13, `Demo.PostgreSQL.Chinook.Tests` 6, `AdventureWorks.Tests` 5 pass.
