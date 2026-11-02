---
type: feature
priority: medium
created: 2026-11-01
closed: 2026-11-01
---
# Store relative paths in lower case in `dblive`

## Release note
Relative paths in the `dblive` tables are stored in lower case. A file renamed only by case (`GetUser.sql` → `getUser.sql`) is no longer a new code item, and a project behaves the same on SQL Server and PostgreSQL. Existing rows are converted on the first deployment. Logs, exceptions and Test Explorer keep the real casing; only the downgrade shows the paths read from the database, in lower case.

## Goal
DbLive identifies code items, test runs and BeforeDeploy/AfterDeploy items in the database by `relative_path`, and the comparison depends on the engine: SQL Server compares by the collation of the database (usually case-insensitive), PostgreSQL is case-sensitive. Files are already found ignoring case (0096-31, kept in 0096-31-01), so the model is inconsistent:
- A file or folder renamed only by case (`GetUser.sql` → `getUser.sql`) is a new code item on PostgreSQL or with a case-sensitive SQL Server collation: it is redeployed, and the old row stays in `dblive.code`.
- Before 0096-31-01, on Windows, the folder part of a relative path took its casing from the settings (`"CodeFolder": "code"` with the folder `Code` gave `code/x.sql`). After 0096-31-01 it takes the real names, so such a database sees its code items as renamed once.
- The same project behaves differently on SQL Server and PostgreSQL.

With lower-case paths in `dblive`, identification ignores case everywhere, like file discovery.

## Design
- `relative_path` is stored in lower case in `dblive.code`, `dblive.unit_test_run`, `dblive.folder_items` and `dblive.migration` (in the last one it is not a key, but stays consistent with the others).
- Lower-casing happens in SQL, in every query that writes or looks up `relative_path`: `lower(@relative_path)`. Not in C#: `lower()` in the database depends on the collation (SQL Server) or the locale (PostgreSQL), and for non-ASCII names it may differ from `ToLowerInvariant()`. The migration below uses the same `lower()`, so stored and new values always agree within one database. The column itself is not wrapped in `lower()` in the `where` clause, so the primary keys are still used.
- `FileData.RelativePath` keeps the real casing. Logs, exception messages, `UnitTestsFailedException.FailedTests`, test names in Test Explorer and "Go to source" show the real path; on Linux and macOS a lower-case path would not open the file. No C# code compares a path read from the database with a project path (checked 2026-11-01: migrations are matched by version and item type in `MigrationsSaver` and `MigrationChecksumValidator`, and their messages use the project path), so nothing else changes in C#. Only the downgrade shows paths read from the database, because the files may be gone from the project (`DowngradeDeployer.cs:89,108`): its messages will show lower-case paths.
- A new self-deploy migration in each provider, the same shape as the separator migration of 0096-08 (`08.relative-path-separator.sql`, `05.relative-path-separator.sql`): in each table with `relative_path` in the primary key, delete the rows that collide after `lower()` and keep the latest one (same ordering as in the separator migration), then `update ... set relative_path = lower(relative_path)`. On SQL Server with a case-insensitive collation the duplicates cannot exist, so the delete removes nothing.
- The next self-deploy version: today `09` for SQL Server, `06` for PostgreSQL. Independent of 0096-31-01: before it, the new scripts are copied to `bin/` by the provider targets, after it they are embedded.

## Where
- DA, both providers (`src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`): `FindCodeItemAsync`, `SaveCodeItemAsync` (`merge` / `on conflict (relative_path)`), `MarkCodeAsVerifiedAsync`, `SaveUnitTestResultAsync`, `MarkItemAsAppliedAsync` (folder items), `SaveMigrationItemAsync`, `RepairMigrationChecksumAsync`.
- Self-deploy scripts: `src/DbLive.MSSQL/SQL/`, `src/DbLive.PostgreSQL/SQL/`.
- Keys: `pk_dblive_code (relative_path)`, `pk_unit_tests_results (relative_path)`, `pk_dblive_folders (folder_type, relative_path)` (`src/DbLive.MSSQL/SQL/02.code.sql`, `03.unit-tests.sql`, `04.folder-items.sql`; `src/DbLive.PostgreSQL/SQL/01.migration.sql`).
- Tests that read paths from the database: `src/DbLive.MSSQL.Tests/CrossPlatformDeployTests.cs:54,57,99-107`, `src/DbLive.PostgreSQL.Tests/CrossPlatformDeployTests.cs:54,57,99-107`.

## Plan
- [x] Self-deploy migration for SQL Server and PostgreSQL: delete duplicates, lower-case `relative_path` in the four tables.
- [x] `lower(@relative_path)` in every DA query that writes or looks up `relative_path`, in both providers.
- [x] Check that nothing shown to the user takes the path from the database where the project path is available.

### Tests
- [x] Integration, both providers: a database with rows for `Code/x.sql` and `code/x.sql` (and the same in `unit_test_run` and `folder_items`) after self-deploy has one row per file, in lower case, the latest one kept. On SQL Server create the test database with a case-sensitive collation (for example `Latin1_General_100_CS_AS`), otherwise the duplicates cannot exist.
- [x] Integration, both providers: a code file renamed only by case is not redeployed (`applied_utc` unchanged) and leaves one row.
- [x] Integration: a database deployed with `Code/...` paths, then deployed with `"CodeFolder": "code"` in the settings, redeploys nothing.
- [x] Unit: `UnitTestsFailedException.FailedTests` and log messages keep the real casing.
- [x] Update the assertions on paths read from the database in `CrossPlatformDeployTests` (both providers).
- [x] All tests pass, including the Docker integration tests and the demo tests.

## Docs to update
- `docs/llms-full.txt` §15 (`dblive` schema): "`relative_path` is relative to the scripts root and uses `/` on every OS" → also lower case; older databases are converted automatically, the latest row is kept when two rows differ only by case.
- `docs/llms-full.txt` §6 (code objects) and §8.1 (tests): "identified by its relative path", with examples that still show `\` (`Code\dbo\GetUser.sql`, `Tests\Orders\select-order.test.sql`): fix the separator and say that case is ignored; a file renamed only by case keeps its identity.
- `docs/deployment/code.md:7`: the same example with `\`.

## Done when
- [x] `relative_path` is lower case in all four `dblive` tables of a new and of an upgraded database, on SQL Server and PostgreSQL
- [x] A rename that changes only case redeploys nothing
- [x] Logs, Test Explorer and "Go to source" show the real casing
- [x] Docs updated

## Notes
2026-11-01, decided with the maintainer while reviewing 0096-31-01 (open question on the casing of `RelativePath`). Options considered:
- Keep the real casing in `dblive` and compare with `lower()` on both sides, with a unique index on `lower(relative_path)`. Keeps the paths in `dblive` readable, but changes every query and the keys. Not chosen.
- Lower case in C# (`ToLowerInvariant()`). May differ from the database's `lower()` for non-ASCII names, which the migration has to use. Not chosen.
- Cost of the chosen option: paths in the `dblive` tables are in lower case (`code/dbo/getuser.sql`), so they are a little harder to read there.

2026-11-01, while implementing:
- SQL Server migration: the `update` is limited with `where relative_path collate Latin1_General_100_BIN2 <> lower(relative_path)`. Without the binary collation a case-insensitive database sees `Code/x.sql` and `code/x.sql` as equal and would skip the row.
- SQL Server `CrossPlatformDeployTests` creates its database with `Latin1_General_100_CS_AS` for the whole class (DbLive skips creating a database that exists), so it behaves like PostgreSQL. Verified: with `lower()` removed from `MsSqlDA` four of its tests fail; with the default case-insensitive collation they would pass anyway.
- After 0096-31-01 the casing of `"CodeFolder"` no longer reaches the path, so the settings test simulates a database deployed by an older DbLive on Windows: `code/dbo/V1.sql` stored (folder from the settings, file name from the disk), then a deploy with the new DbLive finds `Code/dbo/V1.sql` and redeploys nothing.
- A case-only rename of a folder is not tested separately: in a temp folder on Windows it cannot be done reliably, and for the database it is the same lower-cased path as a file rename.
- Found: two project files whose paths differ only by case (possible on Linux) now share one row in `dblive`. Split out as 0096-31-02-01.

## Result
- New self-deploy scripts `src/DbLive.MSSQL/SQL/09.relative-path-lower-case.sql` and `src/DbLive.PostgreSQL/SQL/06.relative-path-lower-case.sql`: in `dblive.code`, `dblive.unit_test_run` and `dblive.folder_items` the rows that collide after `lower()` are deleted (the latest one kept, same ordering as the separator migration), then `relative_path` is lower-cased in these tables and in `dblive.migration`.
- `MsSqlDA` and `PostgreSqlDA`: every query that writes or looks up `relative_path` uses `lower(@relative_path)`; the column is compared as is, so the primary keys are used. C# keeps the real casing (`FileData.RelativePath`), so logs, exceptions, `UnitTestsFailedException.FailedTests`, Test Explorer and "Go to source" are unchanged. Only the downgrade shows paths read from the database (lower case).
- Tests: `CrossPlatformDeployTests` in both providers (updated assertions on stored paths; new: lower case in a new database, migration of case duplicates, case-only rename, older Windows database with the settings casing, failed test reported by its real path); unit tests in `CodeItemDeployerTests` and `UnitTestsRunnerTests` for the real casing. All 400 tests pass, including the Docker integration tests and the demo tests.
- Docs: `docs/llms-full.txt` §3.4, §6, §8.1, §15; `docs/deployment/code.md`, `docs/deployment/index.md`, `docs/project/index.md`.
