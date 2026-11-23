---
type: docs
priority: medium
created: 2026-11-23
---
# The minimum versions of SQL Server and PostgreSQL are not stated

## Problem
No doc says which server versions DbLive supports: §2 and §19 of `docs/llms-full.txt` ("Supported engines: SQL Server / Azure SQL and PostgreSQL"), the root `README.md` and the package READMEs give no versions. Two places disagree with each other and with the code:
- §13.3 (`docs/llms-full.txt:970`) and `docs/unit-testing/deployment-tests.md:57`: the schema check of the deployment test needs "PostgreSQL 12 or later".
- §13 (`docs/llms-full.txt:502`), the recipe for dropping a test database: "PostgreSQL 13 or later".

The code needs:
- **PostgreSQL 13**: every script in a transaction uses `pg_current_xact_id()` and `xid8` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:309`, `:329`), and the check after a test uses `pg_current_xact_id_if_assigned()` (`:25`). These functions appeared in PostgreSQL 13, so on PostgreSQL 12 every deployment fails on its first script in a transaction. Known from the PostgreSQL docs, not run on PostgreSQL 12.
- **SQL Server 2016** for the library: `current_transaction_id()` in `src/DbLive.MSSQL/MsSqlDA.cs`. **SQL Server 2017** for the schema check of the deployment test: `string_agg` and `concat_ws` in `MsSqlSchemaSnapshot.cs`. `tasks/done/0096-87-...` says "DbLive requires SQL Server 2017 or later", but no doc says it.

`docs/versioning.md` makes dropping a supported engine version a breaking change, so the minimum must be stated before 2.0.

## Fix
State the minimum versions in one place (§2) and link to it from the other places.

- [ ] SQL Server minimum: 2016 (what the library needs) or 2017 (one number together with the deployment test)?

## Docs to update
- `docs/llms-full.txt` §2, §13.3 (line 970), §19
- `docs/unit-testing/deployment-tests.md:57`
- `README.md`, `src/DbLive.MSSQL/README.md`, `src/DbLive.PostgreSQL/README.md`
- `docs/index.md` or `docs/gettingstarted.md`, where the requirements are listed

## Done when
- [ ] Docs updated
