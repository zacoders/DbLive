---
created: 2026-11-07
---
# Run the statements of a PostgreSQL script one by one

## Idea
DbLive sends a PostgreSQL script to the server as one command (`PostgreSqlDA.ExecuteNonQueryAsync`). Npgsql sends its statements in one batch with one Sync, so PostgreSQL runs them as one implicit transaction, also when the item has `"TransactionWrapLevel": "None"`. Split the script into statements on the client and execute each one as its own command, as DbUp, Flyway and Liquibase do. Without a transaction each statement then commits on its own:

- an update in batches is plain statements, or a loop in a `do` block next to other statements;
- `create index concurrently` can share a file with other statements: `set lock_timeout`, a second index, and the `do` block that drops the index left invalid by a failed run (today that block goes to a BeforeDeploy script).

Came up 2026-11-07 in the documentation of long-running migrations (#96, `docs/deployment/migrations.md`). The maintainer would most likely take the approach of DbUp, and is open to other parsers.

## Today
Verified 2026-11-07 on PostgreSQL 18.0 with Npgsql 10.0.1, deployed through DbLive (a temporary test, not kept). Migration 002 with `"TransactionWrapLevel": "None"` unless stated otherwise:

| 002.migration.sql | Result |
|---|---|
| two `update` statements | deployed; the rows have one `xmin`: one transaction |
| a `do` block with `commit` after each of 10 batches, alone in the file | deployed; 10 `xmin`: 10 transactions |
| `--` and `/* */` comments, then the `do` block | deployed; 10 transactions |
| `call` of a procedure that commits after each batch | deployed; 10 transactions |
| `select 1;`, then the `do` block | `2D000: invalid transaction termination`; nothing applied |
| the `do` block, without the settings file (a migration transaction) | `2D000: invalid transaction termination` |
| two `create index concurrently if not exists` | `25001: CREATE INDEX CONCURRENTLY cannot run inside a transaction block`; the first index is left with `indisvalid = false` |
| the same without the settings file | `25001`; no index left |
| after the failure above: one index per file, deployed again | deployed; the first index is skipped by `if not exists` and stays invalid |
| a `do` block that drops the invalid index, then `create index concurrently if not exists` | `25001: CREATE INDEX CONCURRENTLY cannot run inside a transaction block`; the `drop` is rolled back, the index stays invalid |
| the same `do` block in `BeforeDeploy`, the index alone in the migration file | after the failure, the next deployment drops the invalid index and the migration builds it again (valid) |

Not related to the split: `drop index concurrently` in a `do` block fails with `25001: DROP INDEX CONCURRENTLY cannot be executed from a function`.

The docs describe these limits (`docs/llms-full.txt` §16 and §18.7, `docs/project/configuration.md` "Item settings"): one such statement per file, batches in a `do` block, the drop of an invalid index in BeforeDeploy. The maintainer decided (2026-11-07) that an index left invalid by a failed concurrent build is PostgreSQL behavior and the developer's to handle; DbLive documents it and does not check index validity.

## Other tools
| Tool | How it runs a PostgreSQL script | Transaction control |
|---|---|---|
| DbUp (`dbup-postgresql`) | `PostgresqlConnectionManager.SplitScriptIntoCommands` splits with `PostgresqlQueryParser.ParseRawQuery`; `ScriptExecutor` runs one `DbCommand` per statement | `WithoutTransaction()` (default), `WithTransactionPerScript()`, `WithTransaction()`, for the whole run; no per-script option found |
| Flyway | its own parser, statement by statement | a transaction per migration; detects statements that cannot run in a transaction and runs that migration without one; mixing both kinds is an error unless `mixed=true`; `executeInTransaction` per script in a `.conf` file |
| Liquibase | `splitStatements` (default `true`) | `runInTransaction: false` per changeset |
| golang-migrate | the whole file in one `Exec` by default: one implicit transaction, like DbLive | `x-multi-statement=true` splits on `;` without a parser (breaks on `;` in a string) |
| Atlas | a transaction per file | `-- atlas:txmode none` in the file; with `none` the next run resumes from the failed statement |

DbUp's parser, as read on GitHub 2026-11-07 (not tried): about 330 lines, "based on original code from Npgsql" `src/Npgsql/SqlQueryParser.cs` under Npgsql's BSD-style license (the license file is in the DbUp repository). It handles `$$` and `$tag$` quoting, `'...'` with or without `standard_conforming_strings`, `E'...'`, `--` and nested `/* */` comments, quoted identifiers, and `;` inside parentheses. It does not handle `begin atomic` bodies (SQL-standard function bodies, PostgreSQL 14+): a `;` inside one ends the statement.

## Open questions
- [ ] Which parser: DbUp's `PostgresqlQueryParser` (MIT, derived from Npgsql), Npgsql's `SqlQueryParser` taken directly, or another one (Flyway's PostgreSQL parser; psql's lexer `psqlscan.l`, the reference behavior; libpg_query, the real PostgreSQL parser as a native library)? A license notice in the repository either way. Does Npgsql expose its parsing in a public API (not checked)?
- [ ] `begin atomic`: support it (track `begin atomic` ... `end` like a quoted section), or document the limit?
- [ ] Scope. Migration and breaking scripts with `None` gain the most. In a transaction, splitting changes nothing but the error report. Code files, Code breaking scripts, BeforeDeploy and AfterDeploy run without a transaction and are atomic today (documented in §16): split, a failed file stays partly applied. SQL tests read several result sets of one command (`ExecuteQueryMultipleAsync`) and run in a transaction anyway: leave them as they are?
- [ ] Breaking change? A file with `None` and several statements fails as a whole today; after the change the statements before the failing one stay applied. The docs already require such a script to be re-runnable, but a user may rely on it. `breaking: true`, or an opt-in setting first?
- [ ] The error report: the position PostgreSQL returns is relative to the statement. Name the failed statement (its number and line in the file).
- [ ] All statements of a script on one connection, so that session settings (`set lock_timeout`, `set statement_timeout`) apply to the next statements. The command timeout: per statement or for the whole script?
- [ ] `NpgsqlBatch` with one command per statement does not help on its own: one Sync at the end makes it one implicit transaction too (the Npgsql maintainer on pgsql-hackers; not verified with Npgsql 10).

## Docs to update when taken
- `docs/llms-full.txt` §16 (script execution), §5.3, §18.7
- `docs/project/configuration.md` ("Item settings")
- `docs/deployment/migrations.md` ("Long-running migrations"), `docs/examples/table-split.md` ("Moving the existing data")

## Sources
- DbUp: https://github.com/DbUp/dbup-postgresql/blob/main/src/dbup-postgresql/PostgresqlConnectionManager.cs, https://github.com/DbUp/dbup-postgresql/blob/main/src/dbup-postgresql/PostgresqlQueryParser.cs, https://github.com/DbUp/DbUp/blob/master/src/dbup-core/Support/ScriptExecutor.cs, https://dbup.readthedocs.io/en/latest/more-info/transactions/
- Flyway: https://documentation.red-gate.com/fd/migration-transaction-handling-273973399.html
- Liquibase: https://docs.liquibase.com/concepts/changelogs/attributes/run-in-transaction.html
- Atlas: https://atlasgo.io/versioned/apply
- Npgsql batches and implicit transactions: https://www.postgresql.org/message-id/CADT4RqDdo9EcFbxwB_YO2H3BVZ0t-1qqZ%3D%2B%2BdVMnYaN6BpyUGQ%40mail.gmail.com
