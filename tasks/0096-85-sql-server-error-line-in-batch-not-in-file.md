---
type: bug
priority: low
created: 2026-11-16
---
# On SQL Server an error names its line in the `GO` batch, not in the file

## Problem
SQL Server counts the line of an error (`SqlError.LineNumber`) from the start of the batch that failed, and `DbLiveSqlException.SqlError` (`Msg ..., Level ..., State ..., Line N`) passes that number on as it is. In a file of several `GO` batches the line does not point to the line of the file: the user has to find the batch first, and the error does not say which batch it was. The todo in `MsSqlDA.ExecuteScriptAsync` says so; not verified by an experiment.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs` (`ExecuteScriptAsync`: the todo; `GetSqlServerError`: `Line {err.LineNumber}`)

## Fix
Needs 0096-84: after it DbLive runs the batches of a file itself, with their texts from the capture mode of SMO. Each batch is a piece of the file as it is (verified in 0096-84), so the line where a batch starts is found by searching the texts in the file one after another, and the line in the file is that line plus `LineNumber` minus one.

- [ ] `GO <count>` gives the same text several times, and two batches with the same text separated by a plain `GO` give the same list of texts. Read the separator line after a batch in the file (`GO` and its count) to tell the repetitions, which start at the same line, from the next batch?
- [ ] Where does the line of the file go: instead of `Line N` in `SqlError`, or next to it (the line in the batch and the line in the file)?
- [ ] An error inside a procedure, function or trigger that the batch calls: `LineNumber` is the line in that object, and `Procedure` names it. Leave such a line as it is?

## Docs to update
- `docs/llms-full.txt` §11.2 (`DbLiveSqlException`: "`SqlError` holds provider details: number, severity, line")

## Done when
- [ ] The error of a file of several batches names the line in the file; integration test: an error in the second batch, and in a batch after `GO 2`
- [ ] Docs updated

## Notes
- Came up 2026-11-16 in 0096-84; the maintainer decided to make it a task of its own (2026-11-16).
- Before 0096-84 SMO runs the batches, and its error does not say which batch failed: the line in the file cannot be found there.
