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

## Depends on
- `tasks/done/0096-32-undo-test-schema-compare.md` (the schema snapshot and its diff: missing, extra, changed)
- `tasks/0096-46-remove-undo.md` (why: a rollback relies on expand/contract)

## See also
- `tasks/ideas/migration-step-tests.md`: tests of each step of a version on prepared data; it deploys the versions one by one too
