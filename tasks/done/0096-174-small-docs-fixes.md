---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-25
---
# Small docs fixes from the second review of 2026-11-24

## Release note
None. Corrections of the XML docs of `IDbLiveTester`, `IDbLive` and the container fixture builders, of the READMEs of `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL`, of the docs pages and of a release note of this release; DbLive behaves as before.

## Problem
Statements of the docs that the code does not match, each small.

1. **One container "per test run".** The container fixture builders keep their containers in a static dictionary, one per image per test process. `dotnet test` of a solution with two test projects runs two processes and starts two containers. `docs/llms-full.txt` §11.5 (line 860) says "per test process", which is right; these say "per test run":
   - the XML doc of `src/DbLive.Testcontainers.MSSQL/MsSqlContainerFixtureBuilder.cs:8` and `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs:8`;
   - line 3 of `src/DbLive.Testcontainers.MSSQL/README.md` and `src/DbLive.Testcontainers.PostgreSQL/README.md`;
   - `docs/unit-testing/index.md:218` ("one container per test run", "when the test run ends").
   Read in the code, not run.
2. **The XML doc of `IDbLiveTester`** (`src/DbLive/Testing/IDbLiveTester.cs`):
   - line 16: `DatabaseRequiresNewerDbLiveException` "this one cannot write its tables". The tester writes nothing to the `dblive` tables; §15: a newer DbLive changed them "in a way this one cannot read".
   - the `<exception>` list of `RunAllTestsAsync` lacks `NotSupportedException`: `Pooling=False` on SQL Server and `Enlist=False` (§8.2, §17).
   - line 24: `writeLine` "gets ... what the test returned or why it failed". A test that passed writes only `Success` (`UnitTestItemRunner.cs:79`, `DbLiveTester.cs:93`); the result sets of the test are not written.

## Docs to update
- XML docs of `MsSqlContainerFixtureBuilder`, `PostgreSqlContainerFixtureBuilder`, `IDbLiveTester`, `IDbLive`
- `src/DbLive.Testcontainers.MSSQL/README.md`, `src/DbLive.Testcontainers.PostgreSQL/README.md`
- `docs/unit-testing/index.md` (container builders; "A test database without xUnit")
- `docs/llms-full.txt` §11.5 (container builders)
- the release note of `tasks/done/0096-30-02-fixture-builders.md`

## Done when
- [x] 1–2 fixed

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
- More places said "test run" than the review listed: the XML doc of `GetBuilderAsync` of both container builders ("every call of the test run gets its exception"), line 15 of both READMEs ("Fixtures that start at the same time share one container; ... when the test run ends": all fixtures of the process share it, not only those that start together), `docs/unit-testing/index.md` "A test database without xUnit" ("when the test run ends"), §11.5 ("every fixture of the run fails"), and the release note of `0096-30-02` ("once per test run").
- `IDbLive.DeployAsync` lacked `NotSupportedException` in its `<exception>` list too: the same check of the connection string runs at its start (§12). Added with the same text.

## Result
Docs only, no code changed.
- Container builders: "per test process" instead of "per test run" in the XML docs of the classes and of `GetBuilderAsync`, in both package READMEs, in `docs/unit-testing/index.md` and in the release note of `0096-30-02`. Each place that names the scope says that `dotnet test` runs each test project in a process of its own, with a container of its own; §11.5 says that two test projects start two containers. The container is removed "when the process ends".
- `IDbLiveTester`: `DatabaseRequiresNewerDbLiveException` reads "changed its tables in a way this one cannot read", as §15; `NotSupportedException` for `Pooling=False` on SQL Server or `Enlist=False` added to `RunAllTestsAsync`, and to `IDbLive.DeployAsync`; `writeLine` gets the path of `setup.test.sql` when the test has one, then `Success` or why the test failed, not the result sets.

Tests: none, no code changed. `DbLive`, `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` build without warnings.
