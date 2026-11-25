---
type: bug
priority: medium
created: 2026-11-25
---
# PostgreSQL: a function or procedure with a `begin atomic` body cannot be deployed

## Problem
PostgreSQL 14 brought the SQL-standard body of a `language sql` function or procedure:

```sql
create or replace function add_numbers(a int, b int) returns int
language sql
begin atomic
  select a + b;
end;
```

DbLive sends a PostgreSQL script as one `NpgsqlCommand`, and Npgsql splits its text into statements at every `;` outside quotes, comments and parentheses. Its parser does not know `begin atomic ... end`, so the `;` inside the body cuts the statement, and the file fails on every deployment with `42601: syntax error at end of input`, inside `CodeDeploymentAggregateException`. Nothing in the message points to the cause. A body without the inner `;` is rejected by the server, so the form cannot be deployed at all, in a code file, a migration or any other script. Verified by the review of 2026-11-25: a `DeploymentTest` on PostgreSQL 18 with the file above fails so, and plain Npgsql 10.0.1 fails on a body of one and of two statements.

What works: the body as a string (`as $$ select a + b; $$`), and `return a + b` (one expression, no `;` inside).

It matters for the brownfield recipe (§18.2: script the existing functions into `Code/`): a function created with `begin atomic` is dumped with it (`pg_dump`, `pg_get_functiondef`).

The docs do not mention it:
- §16, "Script execution": "Whole file sent as one command".
- §6, the PostgreSQL code file example, and `docs/deployment/code.md` (PostgreSQL row, line 15).
- §19, Known limitations.
- §11.2 says "Npgsql sends the statements of a file one by one", without what follows from it.

`tasks/ideas/postgresql-split-script-into-statements.md` (lines 44 and 48) names `begin atomic` only as a limit of DbUp's parser for that idea, as if the current one-command way handled it.

## Where
`src/DbLive.PostgreSQL/PostgreSqlDA.cs:292` (`CreateScriptCommand`): the whole file as the text of one `NpgsqlCommand`.

## Fix
- [ ] Which one? (a) Document the limit and the workaround (`as $$ ... $$`, or `return` for one expression) in §16, §19, §6 and `docs/deployment/code.md`, and correct the idea file. (b) Also fail with a clear message: a PostgreSQL script with `begin atomic` (outside comments and strings) fails at the load of the project, or its `42601` error gets a hint that names `begin atomic`. (c) Split the script with a parser of DbLive that knows `begin atomic` (the idea `postgresql-split-script-into-statements.md`); not for 2.0.

## Docs to update
- `docs/llms-full.txt` §6, §11.2, §16, §19
- `docs/deployment/code.md` (PostgreSQL)
- `tasks/ideas/postgresql-split-script-into-statements.md` (`begin atomic`)

## Done when
- [ ] Decided, docs updated (and the check, for (b), with a test on PostgreSQL)

## Notes
- Found by the code review of 2026-11-25.
- `create rule ... do also (a; b)` deploys: the parser of Npgsql tracks parentheses (verified by the review).
