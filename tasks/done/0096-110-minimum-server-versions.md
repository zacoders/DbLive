---
type: docs
priority: medium
created: 2026-11-23
closed: 2026-11-23
breaking: true
---
# The minimum versions of SQL Server and PostgreSQL are not stated

## Release note
**DbLive requires SQL Server 2016 SP1 or later and PostgreSQL 13 or later**; Azure SQL Database and Azure SQL Managed Instance are supported. DbLive reads the ID of the transaction before and after every script that runs in a transaction, with `current_transaction_id()` on SQL Server and `pg_current_xact_id()` on PostgreSQL, which came in PostgreSQL 13. The minimum versions are listed under "Requirements" in the documentation (https://zacoders.github.io/DbLive/#requirements); raising them is a breaking change (https://zacoders.github.io/DbLive/versioning/).

To upgrade: on PostgreSQL 12 or older, upgrade the server first. Otherwise the first deployment fails with `42883: function pg_current_xact_id() does not exist`.

## Problem
No doc says which server versions DbLive supports: §2 and §19 of `docs/llms-full.txt` ("Supported engines: SQL Server / Azure SQL and PostgreSQL"), the root `README.md` and the package READMEs give no versions. Two places disagree with each other and with the code:
- §13.3 (`docs/llms-full.txt:970`) and `docs/unit-testing/deployment-tests.md:57`: the schema check of the deployment test needs "PostgreSQL 12 or later".
- §13 (`docs/llms-full.txt:502`), the recipe for dropping a test database: "PostgreSQL 13 or later".

The code needs:
- **PostgreSQL 13**: every script in a transaction uses `pg_current_xact_id()` and `xid8` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:309`, `:329`), and the check after a test uses `pg_current_xact_id_if_assigned()` (`:25`). These functions appeared in PostgreSQL 13, so on PostgreSQL 12 every deployment fails on its first script in a transaction (verified, see Notes).
- **SQL Server 2016** for the library: `current_transaction_id()` in `src/DbLive.MSSQL/MsSqlDA.cs`. **SQL Server 2017** for the schema check of the deployment test: `string_agg` and `concat_ws` in `MsSqlSchemaSnapshot.cs`. `tasks/done/0096-87-...` says "DbLive requires SQL Server 2017 or later", but no doc says it.

`docs/versioning.md` makes dropping a supported engine version a breaking change, so the minimum must be stated before 2.0.

## Fix
State the minimum versions in one place (§2) and link to it from the other places.

- [x] SQL Server minimum: 2016 (what the library needs) or 2017 (one number together with the deployment test)? 2016, the maintainer, 2026-11-23: "make SQL Server 2016 work". The schema snapshot builds its lists without `string_agg` and `concat_ws`. Stated as 2016 SP1: `create or alter`, which the docs and the template use for code files, came in SP1.
- PostgreSQL 13, the maintainer, 2026-11-23: runs DbLive 1.5 on PostgreSQL 10 and upgrades the server for 2.0.

## Docs to update
- `docs/llms-full.txt` §2, §13 (the recipe for dropping a test database), §13.3, §19
- `docs/index.md` ("Requirements", new), `docs/versioning.md`, `docs/unit-testing/deployment-tests.md`, `docs/unit-testing/index.md`
- `README.md`, `src/DbLive.MSSQL/README.md`, `src/DbLive.PostgreSQL/README.md`
- `Development.md` ("Minimum server versions", new)
- The release note of `tasks/done/0096-32-undo-test-schema-compare.md` named SQL Server 2017 and PostgreSQL 12

## Done when
- [x] The schema snapshot of SQL Server runs on SQL Server 2016, with the same output
- [x] Docs updated

## Notes
- Verified on SQL Server 2025: the new snapshot query returns the same rows as the old one, character for character, on a schema of 121 objects (names with `&`, `<`, `>`; memory-optimized and temporal tables; masked, hidden, sparse, rowguidcol and computed columns; columnstore, filtered, disabled and `ignore_dup_key` indexes; disabled, not trusted and `not for replication` constraints; multi-column foreign keys; table types with indexes; alias types; sequences; synonyms; database DDL triggers). `SchemaSnapshotTests` and all 157 tests of `DbLive.MSSQL.Tests` pass.
- Verified: `for xml path(''), type` with `.value('(./text())[1]', 'nvarchar(max)')` keeps `&`, `<`, tab, CR LF and leading and trailing spaces. A character below 0x20 other than tab, CR and LF fails it with Msg 6841; `string_agg` had no such limit. It matters only for such a character in the name of an index or foreign key column, or in a column definition of a table type. Not handled: nobody names a column so.
- Verified: xml methods need `QUOTED_IDENTIFIER ON` (Msg 1934 in `sqlcmd` without `-I`). Connections of Microsoft.Data.SqlClient have it on, and `MsSqlDA` already uses xml methods for `#paths`.
- Not run on SQL Server 2016: there is no Linux image of it, and no instance on the development machine. What the snapshot reads exists in 2016 by the docs of SQL Server: `sys.masked_columns`, `generated_always_type`, `is_hidden`, `temporal_type`, `history_table_id`; `current_transaction_id()` too.
- Verified on `mcr.microsoft.com/mssql/server:2017-latest`, the oldest SQL Server image: 156 of 157 tests of `DbLive.MSSQL.Tests` pass, `SchemaSnapshotTests` and `SchemaCheckTests` among them. The failure is in the test: `SqlTestResultSetTests.Column_types_are_named_as_in_TSql` creates columns of the types `json` and `vector` (SQL Server 2025).
- Verified on `postgres:13`: 80 of 83 tests of `DbLive.PostgreSQL.Tests` pass. The 3 failures are in the tests, not in DbLive: `pg_terminate_backend(pid, timeout)` and `idle_session_timeout` came in PostgreSQL 14 (DbLive calls `pg_terminate_backend(pid)`), and `pg_get_viewdef` prints `SELECT person.id, person.name ...` where PostgreSQL 18 prints `SELECT id, name ...` (a snapshot is compared only with one from the same server).
- Verified on `postgres:12`: 66 tests fail with `42883: function pg_current_xact_id() does not exist` (`DbLiveSqlException`), and `drop database ... with (force)` of the recipe with a syntax error.
- `0096-32` chose SQL Server 2017 on purpose: every SQL Server Docker image is 2017 or later, and 2016 is out of extended support since July 2026. The maintainer reversed it here.
- The tests still run on the newest images only. A CI job on the minimum images was not added; `Development.md` says which SQL is missing on SQL Server 2016 and how to run the tests on an older image.

## Result
- `MsSqlSchemaSnapshot` runs on SQL Server 2016: lists are built with `for xml path` instead of `string_agg`, optional parts are joined with `concat` and `' ' + x` instead of `concat_ws`. The output is the same (Notes).
- The minimum versions are stated in §2 of `docs/llms-full.txt` (with the reasons and the error of an older server), in "Requirements" of `docs/index.md`, in `README.md` and in the package READMEs. §13.3, §19, `deployment-tests.md` and `versioning.md` link to them; `versioning.md` says that raising a minimum version is a breaking change. The recipe for dropping a test database no longer says "PostgreSQL 13 or later": every supported version has `with (force)`.
- `Development.md`, "Minimum server versions": the functions missing on SQL Server 2016, how to run the tests of a provider on an older image.
- The release note of `0096-32` no longer names versions.
- Tests: all tests of `DbLive.MSSQL.Tests` on SQL Server 2025; the comparison of the old and new snapshot query; the provider tests on `postgres:13`, `postgres:12` and SQL Server 2017 (Notes).
