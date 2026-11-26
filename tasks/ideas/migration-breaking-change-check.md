---
created: 2026-11-06
---
# Find breaking changes in a release by comparing the schema

## Idea
Without undo scripts (0096-46) a rollback relies on expand/contract: the `Default` deployment of a release (migrations and code) must not break the previous application. That application runs on the new schema until the breaking phase, and a rollback goes back to it. Changes that break it belong in breaking scripts. Nothing checks that today: a `drop column` in a migration passes every test.

The deployment test could check it heuristically with the schema snapshot of 0096-32: compare the schema before and after each migration version, and report what breaks a client written for the schema before.

| Change found | Why it breaks the previous application |
|---|---|
| missing table, view, column, procedure, function, user type, sequence, synonym | the old application uses it |
| renamed object (missing + extra) | the same |
| column type narrowed (length, precision, scale) or changed to another type | writes of the old application fail or lose data |
| column made `not null` | inserts of the old application without the column fail |
| new `not null` column without a default in an existing table | the same; in the diff it looks like an addition, so it needs its own rule |
| default dropped from a `not null` column | the same |
| procedure or function: a parameter removed, renamed or retyped, or a new parameter without a default | calls of the old application fail |
| a column of a view, of a procedure result or of a function result removed or renamed | readers of the old application fail |

Not breaking, not reported: a new table, column, index, procedure or parameter with a default; a widened column; a dropped index; a dropped check, unique or foreign key constraint.

Out of reach of a schema diff: behavior (a procedure that keeps its signature and returns other data, a trigger that rejects writes of the old application), and data (an `update` in a migration that the old application does not expect).

Access only through procedures and functions (the model DbLive recommends) does not reduce the check to code. Code is the contract with the application: the signatures of procedures and functions (parameters, result columns). Tables are the contract with the code, and old code stays in the database until the breaking phase: `GetUser_v1`, which the old application calls, must work on the new schema, and a procedure changed in place runs its old body on the new schema between the commit of the migration and the deployment of the code. So the table rules hold in every access model; with access only through code, the signature rules become the main ones. The only false positive it adds: a dropped column that no code uses at all, and moving that `drop` to a breaking script is the right practice anyway.

Came up 2026-11-06 in the discussion of 0096-46 (remove undo scripts).

## Decided
- 2026-11-06, maintainer: each migration version is compared with the version before it, not with the previous release. A migration must not break the application on its own; a chain of such migrations does not break it either. This holds because every rule above allows only additions and widenings, and those compose. A column added in version 5 and dropped in version 6 of one release is reported: its drop goes to a breaking script.

## Open questions
- [ ] Code has no versions: it is deployed by state after all migrations, and on the empty database of the deployment test there is no earlier code to compare its signatures with. Where does "before" come from for code? (a) The scripts of the previous release: an optional path given to the fixture (CI checks out the last tag), deployed first, then the release; without the path the code check is skipped with a message. It also lifts the known limit of `[SqlDeployFact]` that dropping an `_v1` that really exists is not tested (`docs/llms-full.txt` §13.3). (b) A contract file in the repository with the signatures of procedures and functions (like `PublicAPI.Shipped.txt` of the .NET API analyzers), compared with the deployed code; it works without CI, but a changed line in it, a breaking change, is caught only in review.
- [ ] Error or warning? An error needs a way to accept a known change: a comment in the script, a list in the settings, a setting of the version?
- [ ] New check, unique and foreign key constraints may reject rows the old application writes. Report them, as a warning?
- [ ] The snapshot compares the definition text of procedures and functions, not their parameters or results. Parameters come from the catalog (`sys.parameters`, `pg_proc`); results from `sys.dm_exec_describe_first_result_set_for_object` on SQL Server and the return type on PostgreSQL (not verified).
- [ ] Schema diff (reliable, needs a database, the snapshot already exists) or a text analysis of the scripts (`drop column`, `sp_rename`, `alter column`: works without a database, but misses dynamic SQL and needs a SQL parser for each engine)? The schema diff first.
- [ ] Inside `[SqlDeployFact]`, which then deploys the versions one by one with a snapshot after each (as the undo cycle did before 0096-46), or a run of its own?

## Proposal: run the SQL tests of the previous release
2026-11-26, from a review of the docs by an agent that played a new user (chat with the maintainer), not decided. It builds on (a) of the first open question: the deployment test deploys the scripts of the previous release first. On top of that it runs the SQL tests of that release against the new schema. They are the contract of the previous application, written by the team, so they check behavior, which a schema diff cannot reach ("Out of reach of a schema diff" above).

Steps:
1. Deploy build N (the scripts of the previous release) to a new database.
2. Deploy build N+1 with `DeployParameters.Default`.
3. Run the SQL tests of N and of N+1. A failed test of N: the release breaks the previous application before the breaking phase, so a rollback of the application to N does not work.
4. `DeployParameters.Breaking` of N+1, then the tests of N+1, and the breaking phase again with the schema check, as now. The tests of N do not run after the breaking phase: it drops what N uses.

What it catches that the deployment test on an empty database does not:
- the contract of an object changed in place, without the next version name (rule 14 of `docs/llms-full.txt`): the test of N calls the object with the old parameters or expects the old result columns;
- a migration that breaks the code of N: `GetUser_v1` stays in the database and runs on the new schema;
- a breaking script that drops an `_v1` that really exists (the known limit of `[SqlDeployFact]`, `docs/llms-full.txt` §13.3);
- what an incremental deployment does: the code of N+1 over the code of N, the migrations of N+1 over those of N, as production does.

Limits:
- A test of N fails on a change that does not break the application: a test that reads a table with `select *` and `rows` fails on an added column, and so does an `insert` without a column list in its arrange. Tests through procedures and functions (the "database as a service" model) do not depend on the columns of tables; tests of tables do.
- A bug fix in N+1 changes behavior on purpose, and the test of N that checked the old behavior fails. It needs a way to accept it, the same question as "Error or warning?" above.
- Data: the tests of N run in rolled-back transactions, so the tables are empty when N+1 is deployed. Data migrations stay for `migration-step-tests.md`.
- CI gives the scripts of N: a checkout of the last tag, deployed with `SetScriptsFolder`. The SQL project assembly of N next to the one of N+1 in one process needs an `AssemblyLoadContext` of its own (not verified), so a folder is simpler. Without the scripts of N the step is skipped with a message.

Open questions:
- [ ] A recipe in the docs first? A user can write it today with public API only: a container fixture builder for the database, a builder with `SetScriptsFolder` (N) and one with `SetProject` (N+1) on the same connection string, `DeployAsync` of each, `RunAllTestsAsync` of each tester (not verified).
- [ ] As a feature: part of `DeploymentTest`, with the scripts of N as an optional parameter, or a test of its own?
- [ ] Which tests of N run: all of them, or only the tests in `Code/` next to the interface objects, so that tests of tables give no false failures?
- [ ] Together with the schema diff of this idea, or the tests of N alone first (no new comparison code)?

## Depends on
- `tasks/done/0096-32-undo-test-schema-compare.md` (the schema snapshot and its diff: missing, extra, changed)
- `tasks/0096-46-remove-undo.md` (why: a rollback relies on expand/contract)

## See also
- `tasks/ideas/migration-step-tests.md`: tests of each step of a version on prepared data; it deploys the versions one by one too
