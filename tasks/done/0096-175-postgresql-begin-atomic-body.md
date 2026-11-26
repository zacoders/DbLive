---
type: bug
priority: medium
created: 2026-11-25
closed: 2026-11-25
---
# PostgreSQL: a function or procedure with a `begin atomic` body cannot be deployed

## Release note
PostgreSQL: a function or procedure with the SQL-standard body `begin atomic ... end` (PostgreSQL 14) still cannot be deployed, in a code file or any other script: Npgsql splits the script at the `;` inside the body, and the script fails with `42601: syntax error at end of input`. That error now ends with a line that names `begin atomic` and the forms that deploy: the body as a string (`as $$ ... $$`), or `return <expression>` for one expression. Nothing to change on upgrade.

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
- [x] Which one? (a) Document the limit and the workaround (`as $$ ... $$`, or `return` for one expression) in §16, §19, §6 and `docs/deployment/code.md`, and correct the idea file. (b) Also fail with a clear message: a PostgreSQL script with `begin atomic` (outside comments and strings) fails at the load of the project, or its `42601` error gets a hint that names `begin atomic`. (c) Split the script with a parser of DbLive that knows `begin atomic` (the idea `postgresql-split-script-into-statements.md`); not for 2.0.
  - (a), and from (b) the hint on `42601`, not the check at the load of the project. Decision of the maintainer, 2026-11-25.

## Docs to update
- `docs/llms-full.txt` §6, §11.2, §16, §18.2, §19
- `docs/deployment/code.md` (PostgreSQL)
- `tasks/ideas/postgresql-split-script-into-statements.md` (`begin atomic`)

## Done when
- [x] Decided, docs updated (and the check, for (b), with a test on PostgreSQL)

## Notes
- Found by the code review of 2026-11-25.
- `create rule ... do also (a; b)` deploys: the parser of Npgsql tracks parentheses (verified by the review).
- Npgsql will not learn `begin atomic`: its answer to the SQL-standard body was the raw SQL mode, which does not parse the text (https://www.infoq.com/news/2022/06/PostgreSQL-14-Breaking-Change/). DbUp has the same limit (https://github.com/DbUp/dbup-postgresql/issues/10).
- Not taken:
  - The raw SQL mode for the script. The switch `Npgsql.EnableSqlRewriting` is global to the process: it changes the application that hosts DbLive, and DbLive's own queries with named parameters. Per command it comes only with positional parameters (a dummy `$1`), and then a command takes one statement only: the extended protocol refuses several.
  - Wrapping a script with `begin atomic` into `do $x$ begin execute $q$...$q$; end $x$`: the server's parser would split it right, but the script runs inside a function (`create index concurrently`, `vacuum`, `commit` behave otherwise), and the positions of errors move. Not tried.
  - The check at the load of the project: matched by the words without a lexer, it would refuse a file that has them in a comment or in a string for `execute`.
- The hint is matched by the words `begin atomic` (`\bbegin\s+atomic\b`, any case) anywhere in the text, also in a comment or a string, and only for `42601`: a false match adds a line to a script that failed anyway. The same way as `native_compilation` of In-Memory OLTP on SQL Server (`MsSqlDA.RunsCodeItemWithoutTransaction`).
- Verified 2026-11-25 on `postgres:18`, Npgsql 10.0.1 (`BeginAtomicBodyTests`): the code file of the problem fails with `42601` `syntax error at end of input` and the hint; the same function with `return a + b` deploys and returns 3; a SQL test that creates the function fails with the hint in `ErrorMessage`.

## Result
- `PostgreSqlDA.ScriptError`: the error of a script (`ExecuteScriptAsync`: migrations, code, breaking and folder scripts; `ExecuteQueryMultipleAsync`: SQL tests) ends with `PostgreSqlDA.BeginAtomicHint` on a new line when it is `42601` and the script has the words `begin atomic`, in the message (what a failed SQL test shows) and in `SqlError` (what the message of a deployment error holds).
- Docs: `docs/llms-full.txt` §6 (the limit, the hint, the workaround with an example of `return`), §11.2 (how Npgsql splits a file, the hint), §16 ("Script execution"), §18.2 (rewrite a function dumped with `begin atomic`), §19 (Known limitations); `docs/deployment/code.md` (under the table of re-runnable patterns). The idea `postgresql-split-script-into-statements.md`: the parser of Npgsql has the limit too, and the open question on `begin atomic` says that a parser of DbLive would lift it.
- Tests: `BeginAtomicBodyTests` (a code file: the hint, then the `return` form deploys; a SQL test: the hint in `ErrorMessage`), and `CodeFileFailureTests.Failed_code_file_has_the_error_of_the_server_in_its_message_and_writes_no_row` checks that a `42601` without the words has no hint.
