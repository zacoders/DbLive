---
type: docs
priority: medium
created: 2026-11-23
closed: 2026-11-23
---
# PostgreSQL: the scripts of one transaction share one session

## Release note
None. A correction of the docs written for 2.0; DbLive behaves as before.

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
Document it per provider. Keep the advice to use permanent tables in `setup.test.sql`, because it works on both providers. A PostgreSQL script under `TransactionWrapLevel: Deployment` that changes a setting restores it at its end (`reset search_path;`), or a function that needs a setting takes it in its definition (`create function ... set search_path = s`). Not `set local`: it lasts to the end of the transaction, which under `Deployment` is the deployment (verified, see Notes).

- [x] Only document it, or reset the session state on PostgreSQL before each script in a transaction (`reset all; set role none;`; `discard all` cannot run in a transaction)? Only document it (the maintainer, 2026-11-23). The behavior needs a script that leaves a setting changed, and a deployment test finds what it breaks. A reset would not be complete either (`reset all` leaves prepared statements, `listen`, advisory locks and `session authorization`), so the docs would keep caveats.

## Docs to update
- `docs/llms-full.txt` §8.2 (connections of the setup and the test), §10 (`Deployment`), §16 (a row "session state between the scripts of one transaction")
- `docs/unit-testing/index.md` (section on `setup.test.sql`)
- `docs/project/configuration.md` (`TransactionWrapLevel: Deployment`)

## Done when
- [x] Docs updated
- [x] A PostgreSQL integration test pins the behavior: a temporary table of `setup.test.sql` is visible to the test

## Notes
This settles the open item "Not verified: whether the session state of `setup.test.sql` (temporary tables) is visible to the test on PostgreSQL" in the notes of `0096-documentation.md`.

- The first version of the fix advised `set local` instead of `set` under `Deployment`. It does not help: `SET LOCAL` lasts to the end of the current transaction, and under `Deployment` that is the deployment transaction; the savepoint of a code file is released, which keeps it too. Verified on 2026-11-23 on PostgreSQL 18 (Npgsql 10.0.1) by `SessionStateTests`: with `Deployment`, a migration `create schema s; set local search_path = s;` makes the code file after it create its view in `s`.
- Verified the same way: with `Deployment`, `set search_path = s` in a migration puts the view of the next code file into `s`, and `reset search_path;` at the end of the migration puts it into `public`; with `Migration`, the view is in `public` (the session is reset between the transactions). In a SQL test, a temporary table and `set application_name` of `setup.test.sql` are seen by the test.

## Result
- `docs/llms-full.txt` §8.2: the setup and the test run one after the other in one transaction; session state is not visible on SQL Server (a reset session) and is on PostgreSQL (one session per transaction); permanent tables work on both. §10: a new paragraph "Session state between scripts": SQL Server resets the session of each script; on PostgreSQL the scripts of a transaction and the statements of DbLive in it share the session until it ends, which with `Migration` is one script and its row and with `Deployment` the rest of the deployment (the `search_path` example); `SET LOCAL` does not help; restore the setting at the end of the script or set it on the function. §16: a row "Session state between the scripts of one transaction".
- `docs/unit-testing/index.md` (`setup.test.sql`) and `docs/project/configuration.md` ("Choosing a transaction level", `Deployment`): the same, in short.
- Decision of the maintainer (2026-11-23): only document it, no reset of the session on PostgreSQL.
- Test: `src/DbLive.PostgreSQL.Tests/SessionStateTests.cs`: the temporary table and setting of `setup.test.sql` reach the test; a `search_path` set by a migration reaches the code file with `Deployment` (with `set` and with `set local`), not after `reset search_path;`, and not with `Migration`. No behavior change.
