---
type: chore
priority: low
created: 2026-11-09
---
# Public types of `DbLive` that are not part of the API

## Problem
`DbLive` has 136 public types, 33 of them exceptions. Of the other 103, 67 are named nowhere in `docs/llms-full.txt` and used by no other project of `src` but the tests (verified with grep):

- the item deployers: `CodeItemDeployer`, `MigrationVersionDeployer`, `MigrationItemDeployer`;
- the test runners: `UnitTestsRunner`, `UnitTestItemRunner`, `UnitTestResultChecker`, `AssertInfo`, `AssertType`, `ValidationResult`;
- the classes behind the dependency injection: `ITransactionRunner`, `TransactionScopeManager`, `ISettingsAccessor`, `ITimeProvider`, `IStopWatch`, `IDbLiveSelfDeployer`, `IProjectIdValidator`, `IMigrationVersionValidator`, `ITransactionSettingsValidator`;
- helpers: `StringExtensions`, `ListExtensions`, `HashExtensions`, `ExceptionExtensions`;
- `IInternalDbLiveProject` and `InternalMigration`, internal by name.

16 more are not documented and are used by the provider packages or `DbLive.xunit` (the DTOs of `IDbLiveDA`, `IDbLiveDbConnection`, `IInternalScriptSource`, `Bootstrapper`). Those packages see the internals of `DbLive` through `InternalsVisibleTo`, so they do not need them public.

The types are public because a public class cannot take an internal type in its constructor: once a class of the graph is public, its dependencies must be public too. Not by design. `docs/versioning.md` says that such types may change in any version, so they do not hold back a change. But they show in IntelliSense next to the API, `ConfigureServices` can replace them, and a user who relies on one breaks without warning.

0096-65 makes the phases of the deployment internal (decided 2026-11-09); this task does the rest.

## Where
- All public types of `src/DbLive/`; the same check for `src/DbLive.MSSQL/`, `src/DbLive.PostgreSQL/`, `src/DbLive.xunit/` and the Testcontainers packages
- `docs/versioning.md` (the last paragraph)

## Fix
Make a public type internal unless it is:

- described in the docs: Section 11 of `docs/llms-full.txt`, the exceptions of Section 17, what `docs/versioning.md` covers;
- in the signature of such a type: the models `IDbLiveProject` returns (`Migration`, `CodeGroup`, `GenericItem`, `TestItem`, `FileData`), the types of the members of `IDbLiveDA` that Section 11.4 lists.

The compiler finds the second group: a public member with an internal type does not compile.

### `IDbLiveDA`
It stays public: `DbLiveBuilder.CreateDbLiveDA()` returns it, and Section 11.4 lists 7 of its members (`Provider`, `CreateDBAsync`, `CheckDatabaseExistsAsync`, `DropDbAsync`, `ExecuteNonQueryAsync`, `ExecuteQueryMultipleAsync`, `GetSchemaSnapshotAsync`). Its other members read and write the `dblive` tables (`GetMigrationsAsync`, `SaveCodeItemAsync`, `GetSelfMigrationVersionAsync`, `SetProjectIdAsync`, `IsTransactionUsableAfterErrorAsync`, ...), and their DTOs are public only because the interface is. A provider outside this repository is not supported (decided 2026-11-09), so these members move to an internal interface that `MsSqlDA` and `PostgreSqlDA` implement too, and the DTOs become internal. `IDbLiveDA` keeps the members of Section 11.4.

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

RS0026 and RS0027 (overloads with optional parameters) may fire on the existing API (not checked): fix the overloads or turn the rules off in `.editorconfig`, and say which under Notes.

### Open questions
- [x] Is a provider written outside this repository a supported case? Decided 2026-11-09: not at this stage. See `IDbLiveDA` above.
- [x] A test of the public API, so that a new public type shows in the review? Decided 2026-11-09: yes, with `PublicApiAnalyzers` instead of a test, see Tracking of the public API and Notes.

## Docs to update
- `docs/versioning.md` (the last paragraph: which public types the policy does not cover; a provider outside this repository is not supported)
- `Development.md` (the public API files: what to do on RS0016 and RS0017; "Releases": a major release moves `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`)

## Done when
- [ ] `PublicApiAnalyzers` in the six packages, committed with the API as it was before this task in `PublicAPI.Shipped.txt`
- [ ] Every public type is described in the docs or in the signature of a described one; what stayed public and why is under Notes
- [ ] `IDbLiveDA` has only the members of Section 11.4; the members for the `dblive` tables and their DTOs are internal
- [ ] Docs updated

## Notes
- 2026-11-09, maintainer: `PublicApiAnalyzers` instead of a snapshot test with `PublicApiGenerator`. The analyzer fails the build (in the IDE and in the build step of CI, before the tests), a test fails only where the tests run. It makes a removal an explicit `*REMOVED*` line, where a snapshot shows it as one deleted line of a diff. It tracks nullable annotations and needs no code of ours. What it costs: two files per package, and its one-line-per-member format reads a little worse in a diff than the declarations of `PublicApiGenerator`.
