---
type: docs
priority: medium
created: 2026-11-23
---
# PostgreSQL: the scripts of one transaction share one session

## Problem
`docs/llms-full.txt` §8.2 and `docs/unit-testing/index.md` say that `setup.test.sql` and the test run on separate connections, so the test does not see the session state of the setup (temporary tables, variables, `SET` options). That holds on SQL Server only.

On PostgreSQL, Npgsql gives every connection opened in one `TransactionScope` the same server session: the connector stays enlisted until the transaction ends, and `DISCARD ALL` runs only when it goes back to the pool. Verified on 2026-11-23 on PostgreSQL 18 with Npgsql 10.0.1, with a scope like the one of `TransactionScopeManager` (`Serializable`, async flow). The first connection created a temporary table and ran `set search_path = pg_catalog, public`. The second connection had the same `pg_backend_pid()`, saw the temporary table, and had that `search_path`.

Consequences on PostgreSQL:
- A temporary table or a `SET` of `setup.test.sql` is visible to the test. A test that creates a temporary table with the name of one from its setup fails with "already exists" on PostgreSQL only.
- With `TransactionWrapLevel: Deployment`, a session `SET` of one script (`search_path`, `role`, `statement_timeout`) stays for every later script of the deployment and for the writes of DbLive to the `dblive` tables. With `Migration`, the scope of each script ends and the session is reset, so the two wrap levels behave differently.

SQL Server resets the session of each connection taken from the pool and keeps the transaction, so the docs are right there. §6 says "each script gets a reset session" for `set xact_abort on` on SQL Server, and nothing says that PostgreSQL differs.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:84-85`: the comment "Every connection opened in the scope gets the session of the transaction".
- `src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:45-57`: setup and test, two calls in one scope.
- `src/DbLive.PostgreSQL.Tests/CodeRetryInDeploymentTransactionTests.cs:97-111` relies on this behavior.

## Fix
Document it per provider. Keep the advice to use permanent tables in `setup.test.sql`, because it works on both providers. Advise `set local` instead of `set` in PostgreSQL scripts that run under `TransactionWrapLevel: Deployment`.

- [ ] Only document it, or reset the session state on PostgreSQL before each script in a transaction (`reset all; set role none;`; `discard all` cannot run in a transaction)?

## Docs to update
- `docs/llms-full.txt` §8.2 (connections of the setup and the test), §10 (`Deployment`), §16 (a row "session state between the scripts of one transaction")
- `docs/unit-testing/index.md` (section on `setup.test.sql`)
- `docs/project/configuration.md` (`TransactionWrapLevel: Deployment`)

## Done when
- [ ] Docs updated
- [ ] A PostgreSQL integration test pins the behavior: a temporary table of `setup.test.sql` is visible to the test

## Notes
This settles the open item "Not verified: whether the session state of `setup.test.sql` (temporary tables) is visible to the test on PostgreSQL" in the notes of `0096-documentation.md`.
