---
type: docs
priority: low
created: 2026-11-24
---
# Small docs fixes from the second review of 2026-11-24

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
- XML docs of `MsSqlContainerFixtureBuilder`, `PostgreSqlContainerFixtureBuilder`, `IDbLiveTester`
- `src/DbLive.Testcontainers.MSSQL/README.md`, `src/DbLive.Testcontainers.PostgreSQL/README.md`
- `docs/unit-testing/index.md` (container builders)

## Done when
- [ ] 1–2 fixed

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
