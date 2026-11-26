---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# PostgreSQL: `Position` in the `error` column counts from the start of the statement

## Release note
None. A correction of `docs/llms-full.txt`; DbLive behaves as before.

## Problem
§15 of `docs/llms-full.txt` (line 1010) lists `Position` among the fields of a PostgreSQL error without a word about what it counts, while the line numbers of SQL Server get a whole paragraph. Npgsql splits the file into statements, so `PostgresException.Position` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:908`) counts characters from the start of the failed statement, not of the file. In a file of many statements the position points to the wrong place.

The idea `tasks/ideas/postgresql-split-script-into-statements.md` (line 51) proposes to name the failed statement; this task is only the sentence in the docs until then.

## Fix
One sentence in §15: "`Position` counts characters from the start of the failed statement, not of the file."

## Docs to update
- `docs/llms-full.txt` §15

## Done when
- [x] Docs updated

## Notes
- Verified on 2026-11-23 with Npgsql 10.0.1 and PostgreSQL 18 in Docker: `NpgsqlCommand.ExecuteNonQueryAsync` of `select 1;
select 2;
select 3;
select * from no_such_table;
` failed with `Position = 15`, the position of `no_such_table` in its statement; in the text of the command it is 45.

## Result
`docs/llms-full.txt` §15: "`Position` counts characters from the start of the failed statement, not of the file: Npgsql sends the statements of a file one by one." No code change, no tests.
