---
type: bug
priority: low
created: 2026-11-16
closed: 2026-11-16
---
# On SQL Server an error names its line in the `GO` batch, not in the file

## Release note
**SQL Server: the error of a script names the line of the file where its `GO` batch starts.** SQL Server counts the line of an error from the start of the batch that failed, and in a file of several `GO` batches that line did not point to the line of the file. Now `SqlError` of `DbLiveSqlException`, the `error` column of the `dblive` tables and the log have a line above the errors of the server:

```
SQL Server error:
The batch starts at line 4 of the file.
Msg 8134, Level 16, State 1, Line 3
Divide by zero error encountered.
```

The errors of the server stay as they were. An error in the batch itself is on line `start + Line - 1` of the file (line 6 above); an error inside a procedure that the batch calls, or inside dynamic SQL, counts its line from the start of that procedure or text. Nothing to change on upgrade.

## Problem
SQL Server counts the line of an error (`SqlError.LineNumber`) from the start of the batch that failed, and `DbLiveSqlException.SqlError` (`Msg ..., Level ..., State ..., Line N`) passes that number on as it is. In a file of several `GO` batches the line does not point to the line of the file: the user has to find the batch first, and the error does not say which batch it was. The todo in `MsSqlDA.ExecuteScriptAsync` says so. SSMS shows the line of the file for an error of the batch itself, and the line with the start of the batch next to it (`Procedure ..., Line 17 [Batch Start Line 447]`) for an error with `Procedure` (see the questions below).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs` (`ExecuteScriptAsync`: the todo; `ExecuteBatchesAsync`: the batch that failed; `SplitIntoBatches`: the texts of the batches; `GetSqlServerError`: `Line {err.LineNumber}`)
- `src/DbLive.MSSQL.Tests/ErrorColumnTests.cs` (the text of the `error` column)

## Fix
Needs 0096-84: after it DbLive runs the batches of a file itself, and knows which batch failed.

Decision of the maintainer (2026-11-16): the errors of SQL Server stay as they are, and a line above them names the line of the file where the batch starts. One form for every error of every batch, also in a file of one batch:

```
SQL Server error:
The batch starts at line 4 of the file.
Msg 8134, Level 16, State 1, Line 3
Divide by zero error encountered.
```

Not the line of the file instead of the line of SQL Server: the line is not always a line of the batch (see "Why not the line of the file" in Notes), and the start of the batch is always right. Not the form of SSMS either (see the questions below). For an error in the batch itself the line of the file is `start + Line - 1`.

Only the errors of the batches get the line. An error of `begin transaction` or `commit transaction` of a code file, of the isolation level or of the connection is not in a batch of the file. A SQL test (`ExecuteQueryMultipleAsync`) has no `GO` batches and gets no line.

The capture mode of SMO gives the texts of the batches, not the lines where they start, so DbLive counts them. Each text is a piece of the file as it is, up to and including the line end before its `GO` line (verified in 0096-84). The first batch starts at line 1; the next one starts at the start of the current one plus the line ends in its text plus one, the `GO` line.

The repetitions of `GO <count>` are the same batch and start at the same line, and the texts alone do not tell them: `select 1` + `GO 2` and `select 1` + `GO` + `select 1` + `GO` give the same list. So after each batch DbLive reads its `GO` line, which is at a known line of the file: with `GO <count>` the next `<count> - 1` texts are its repetitions, and the start does not move for them.

Decision of the maintainer (2026-11-16, while implementing): not done. DbLive does not read the `GO` line: `GO <count>` is rare, and whoever uses it finds the line of an error by themselves. Each repetition counts as a batch of its own, and the batches after it get lines further down the file than they are. The table below stays as a record of what SMO does.

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

- [x] Where does the line of the file go: instead of the line in the batch (`Msg 207, Level 16, State 1, Line 57`), or next to it (`Line 3 (line 57 of the file)`; SSMS adds `[Batch Start Line N]`)?
  - Proposal of the agent (2026-11-16): instead. The user looks for the line in the file; the line in the batch tells nothing more then.
  - Proposal of the agent (2026-11-16, after the question below and dynamic SQL): next to it, as SSMS. Instead of the line DbLive would have to tell when the line is a line of the batch, and a wrong guess gives a wrong line without a sign of it.
  - Decision of the maintainer (2026-11-16): next to it, as SSMS, `[Batch Start Line N]`. Changed the same day, see the last question.
- [x] Does a compile error of the `create procedure` (function, trigger) of the batch itself come with `Procedure` set, like an error in a called procedure? To verify by an experiment. If it does, its line still counts from the start of the batch, and "`Procedure` is set, keep the line" gives the wrong line in a file like `drop ...` + `GO` + `create or alter procedure ...`, the common case of the Code folder. How to tell it from a called procedure then?
  - Verified by the maintainer on 2026-11-16 in SSMS: yes. A compile error of the `create procedure` of a batch: `Msg 137, Level 15, State 2, Procedure ValidateTimekeepingIds, Line 17 [Batch Start Line 447]`, `Must declare the scalar variable "@dfasdf".` With the start of the batch next to the line DbLive does not need to tell the two apart.
- [x] Is the number of SSMS the lines before the batch, `[Batch Start Line 0]` for the first batch, so that the line in the file is the sum? From the memory of the agent, not verified; to check in SSMS on a script without `GO` and on the second batch of a script.
  - Verified by the maintainer on 2026-11-16 in SSMS, a script of `select 1 / 0` on lines 2, 6 and 8 of the file, in batches 1, 2 and 3, the last one with `GO 2`: `Msg 8134, Level 16, State 1, Line 2`, `Line 6`, `Line 8` twice. SSMS shows the line of the file for an error without `Procedure`, and no `[Batch Start Line]`.
  - Verified by the maintainer on 2026-11-16 in SSMS, a script of five batches in `tempdb`: an error inside a procedure called on line 7 of the file, in batch 3, gives `Msg 8134, Level 16, State 1, Procedure dbo.Test0096_85, Line 3 [Batch Start Line 6]`; a compile error on line 11, in the `create procedure` of batch 4 that starts on line 9, gives `Msg 137, Level 15, State 2, Procedure Test0096_85_bad, Line 3 [Batch Start Line 8]`. The number is the lines of the file before the batch: yes.
- [x] SSMS shows the line of the file for an error without `Procedure`, and the line of SQL Server with `[Batch Start Line N]` for an error with `Procedure` (the questions above). Do the same as SSMS, or the line of SQL Server with `[Batch Start Line N]` for every error (the decision above)?
  - Proposal of the agent (2026-11-16): the same as SSMS. An error looks as it does when the user runs the file in SSMS, and an error of a migration, the common case, names the line of the file at once. Its one wrong line, an error inside dynamic SQL, is wrong in SSMS the same way (verified, see Notes). It is one condition more than the decision above.
  - Decision of the maintainer (2026-11-16): neither. The universal form: the errors of SQL Server as they are, and above them a line with the line of the file where the batch starts (see Fix).

## Docs to update
- `docs/llms-full.txt` §11.2 (`DbLiveSqlException`: "`SqlError` holds provider details: number, severity, line"), §15 (`error`: "`Line <line in the GO batch>`"), §16 ("Error details")
- `docs/deployment/code.md` ("Status": the `error` column)

## Done when
- [x] The error of a batch of a SQL Server script names the line of the file where the batch starts, above the errors of the server; integration tests: an error in the second batch, in a file of one batch (`ErrorColumnTests`). Not in a batch after `GO 2`: the count is not read (decision above)
- [x] The start of each batch is counted right: unit tests on `GO` on the first line, two `GO` in a row, CRLF, a `GO` line with spaces and a comment. Not the forms of `GO <count>`: the count is not read (decision above)
- [x] Docs updated

## Notes
- Came up 2026-11-16 in 0096-84; the maintainer decided to make it a task of its own (2026-11-16).
- Before 0096-84 SMO runs the batches, and its error does not say which batch failed: the line in the file cannot be found there.

### Why not the line of the file
- A compile error of the `create procedure` of the batch itself has `Procedure` set (verified by the maintainer, see the questions above), like an error inside a procedure that the batch calls. The first line counts from the start of the batch, the second from the start of the procedure: DbLive cannot tell them apart without reading the batch.
- An error inside dynamic SQL counts its line from the start of that text, with `Procedure` empty: a line of the file computed from it is wrong. Verified by the maintainer on 2026-11-16 in SSMS: `exec('` + line end + `select 1 / 0;` on line 15 of the file, in a batch that starts on line 13, gives `Msg 8134, Level 16, State 1, Line 14`, the line 2 of the dynamic text plus the 12 lines before the batch. A common case in migrations: `if not exists (...) exec('create view ...')`. `sp_executesql` is not checked.
- SSMS shows the line of the file only for an error without `Procedure`, and the start of the batch next to the line for an error with it (verified by the maintainer, see the questions above).
- `Procedure` of the error inside the called procedure came with its schema, `dbo.Test0096_85`, and of the compile error without it, `Test0096_85_bad` (the same experiment). DbLive shows it as SQL Server gives it.

### Implementation
- Verified on 2026-11-16 by unit tests of a first version that read the count of the `GO` line (SMO 180.10.0; dropped with the count): every form of the `GO` line in the table above gives the batch as many times as the table says, and the empty batch before `GO <count>` is in the list count times too (`GO 2` on the first line, `GO` + `GO 2`).
- Verified by a temporary test (not kept): SMO ends a line at CR alone as well, `select 1` + CR + `GO` + CR + `select 2` is two batches, and the first text ends with the CR. DbLive counts LF, CRLF and CR alone as line ends, as SMO and editors do; counting LF only would give such a file wrong lines.
- `ExecutionTimeoutException` gets no line: its `SqlError` is empty by design (0096-84), and the message of DbLive is the error of the file. The decision covers the errors of SQL Server. Decision of the maintainer (2026-11-16): a timeout does not name its batch, not even in a task of its own: where a timeout happened is rarely needed.
- An error of a batch becomes `DbLiveSqlException` in `ExecuteBatchesAsync`, where the batch is known; the catch of `ExecuteScriptAsync` lets it pass and wraps only the errors outside the batches.
- The first version read the `GO` line with `[GeneratedRegex]`, which needs a `partial` class; at the request of the maintainer (2026-11-16) the splitting moved to a class of its own, `MsSqlBatches`, so that `MsSqlDA` would not become `partial`. Without the count (decision above) there is no regex and no reason for the class: the splitting is back in `MsSqlDA.SplitIntoBatches`, about 20 lines.

## Result
- `MsSqlDA.SplitIntoBatches` returns `ScriptBatch` (internal record: the text and the line of the file where the batch starts). SMO still splits the file in its capture mode; DbLive counts the start lines: the first batch starts at line 1, the next one after the line ends of the text and the `GO` line. Line ends are LF, CRLF and CR alone. The count of `GO <count>` is not read (decision above): after it the lines are further down the file than they are.
- `MsSqlDA.ExecuteBatchesAsync` turns a `SqlException` of a batch (not a timeout) into `DbLiveSqlException` whose `SqlError` has `The batch starts at line <N> of the file.` right after `SQL Server error:`, in every file, also of one batch. The errors of the server are unchanged. Errors outside the batches (`begin`/`commit` of the transaction of a code file, the isolation level, the connection), timeouts and SQL tests (`ExecuteQueryMultipleAsync`) get no line.
- Not breaking: one line is added to a free text; `Msg` lines keep their form. No change to the `dblive` tables: `error` is a text, an older DbLive reads it as before.
- Docs: `docs/llms-full.txt` §11.2, §15, §16; `docs/deployment/code.md`.
- Unit tests (`MsSqlDABatchesTests`): the start lines of batches with LF, CRLF and CR alone, a `GO` line with spaces and a comment, `GO` on the first line and two `GO` in a row, `GO` inside a comment, `GO3`. Integration tests (`ErrorColumnTests`): a file of one batch (`line 1`), an error in the second batch.
- All tests pass: `DbLive.Tests` 524, `DbLive.MSSQL.Tests` 126.
