---
type: docs
priority: low
created: 2026-11-23
---
# PostgreSQL: `Position` in the `error` column counts from the start of the statement

## Problem
§15 of `docs/llms-full.txt` (line 1010) lists `Position` among the fields of a PostgreSQL error without a word about what it counts, while the line numbers of SQL Server get a whole paragraph. Npgsql splits the file into statements, so `PostgresException.Position` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:908`) counts characters from the start of the failed statement, not of the file. In a file of many statements the position points to the wrong place.

The idea `tasks/ideas/postgresql-split-script-into-statements.md` (line 51) proposes to name the failed statement; this task is only the sentence in the docs until then.

## Fix
One sentence in §15: "`Position` counts characters from the start of the failed statement, not of the file."

## Docs to update
- `docs/llms-full.txt` §15

## Done when
- [ ] Docs updated
