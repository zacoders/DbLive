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
Needs 0096-84: after it DbLive runs the batches of a file itself, and knows which batch failed. The capture mode of SMO gives the texts of the batches, not the lines where they start, so DbLive counts them. Each text is a piece of the file as it is, up to and including the line end before its `GO` line (verified in 0096-84). The first batch starts at line 1; the next one starts at the start of the current one plus the line ends in its text plus one, the `GO` line. The line in the file is the start of the batch plus `LineNumber` minus one.

The repetitions of `GO <count>` are the same batch and start at the same line, and the texts alone do not tell them: `select 1` + `GO 2` and `select 1` + `GO` + `select 1` + `GO` give the same list. So after each batch DbLive reads its `GO` line, which is at a known line of the file: with `GO <count>` the next `<count> - 1` texts are its repetitions, and the start does not move for them.

The forms of the `GO` line, verified on 2026-11-16 with SMO 180.10.0 in its capture mode (a temporary program, not kept):

| `GO` line | SMO |
| --- | --- |
| `go\t\t\t   3   `, `  \tGO 3`, `GO 03`: spaces and tabs before `GO`, between `GO` and the count, after the count | the batch three times |
| `GO 3 -- comment`, `GO -- comment`, `GO\t` | a separator; the comment is dropped |
| `GO 0`, `GO 1` | the batch once |
| `GO3` | not a separator: the line stays in the batch |
| `GO /* c */`, `GO 3 /* c */`, `GO -1`, `GO +3`, `GO 3x`, `GO 3 4`, `GO 99999999999` | `BatchParserException`, before any batch runs |

SMO rejects every other form before a batch runs, so the `GO` line that DbLive reads is always one of the valid ones: spaces and tabs, `GO` in any case, an optional count of digits, an optional `-- comment`. A count of `0` is one run.

Decision of the maintainer (2026-11-16): an error inside a procedure, function or trigger that the batch calls keeps its line: `LineNumber` is the line in that object, and `Procedure` names it.

- [ ] Where does the line of the file go: instead of the line in the batch (`Msg 207, Level 16, State 1, Line 57`), or next to it (`Line 3 (line 57 of the file)`; SSMS adds `[Batch Start Line N]`)?
  - Proposal of the agent (2026-11-16): instead. The user looks for the line in the file; the line in the batch tells nothing more then.
- [ ] Does a compile error of the `create procedure` (function, trigger) of the batch itself come with `Procedure` set, like an error in a called procedure? To verify by an experiment. If it does, its line still counts from the start of the batch, and "`Procedure` is set, keep the line" gives the wrong line in a file like `drop ...` + `GO` + `create or alter procedure ...`, the common case of the Code folder. How to tell it from a called procedure then?

## Docs to update
- `docs/llms-full.txt` §11.2 (`DbLiveSqlException`: "`SqlError` holds provider details: number, severity, line")

## Done when
- [ ] The error of a file of several batches names the line in the file; integration test: an error in the second batch, and in a batch after `GO 2`
- [ ] An error inside a called procedure keeps its line in the procedure; integration test
- [ ] Docs updated

## Notes
- Came up 2026-11-16 in 0096-84; the maintainer decided to make it a task of its own (2026-11-16).
- Before 0096-84 SMO runs the batches, and its error does not say which batch failed: the line in the file cannot be found there.
