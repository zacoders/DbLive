---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# An empty test file fails on SQL Server and passes on PostgreSQL

## Release note
**SQL Server: an empty SQL test passes, as on PostgreSQL.** A test file of 0 bytes or of only a UTF-8 BOM, such as a placeholder for a test to write later, failed on SQL Server with the error of SqlClient, `BeginExecuteReader: CommandText property has not been initialized`, which does not say that the file is empty. Now it passes as every test without an assert result set that runs without an error, as it did on PostgreSQL. Nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §8.3 (line 537): "Without an assert result set, the test passes if the script runs without an error." A test file of 0 bytes (a placeholder `Tests/orders/todo.sql`) fails on SQL Server with the error of SqlClient, `BeginExecuteReader: CommandText property has not been initialized`, which does not say that the file is empty. The same file passes on PostgreSQL. A file with only a line break passes on both. Verified by the review of 2026-11-24 (a scratch app on the source projects, Testcontainers `mssql/server:2025-latest` and `postgres:18`, `RunTestAsync` and `RunAllTestsAsync`).

The other scripts are not affected: `ExecuteBatchesAsync` skips a batch of white space, so an empty `setup.test.sql`, code file or migration deploys.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:472-478`: `QueryMultipleAsync` sets `cmd.CommandText = sqlStatement` without a check of the text.
- `src/DbLive.MSSQL/MsSqlDA.cs:300-304`: `ExecuteBatchesAsync` skips a batch of white space ("SqlCommand fails on an empty text").
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:356`: `AddTransactionCheck` appends a statement to the script, so the command of an empty test is never empty.

## Fix
`QueryMultipleAsync` returns no result sets for an empty text, as `ExecuteBatchesAsync` runs nothing for one; the test then passes as on PostgreSQL. A test in `MsSqlDA` tests (or an integration test of an empty test file on both providers).

## Docs to update
None: the docs already say what the fix gives.

## Done when
- [x] Fixed, with a test on SQL Server (and the same test on PostgreSQL)

## Result
- `MsSqlDA.QueryMultipleAsync` returns no result sets for a text of white space, with the check of `ExecuteBatchesAsync` (`string.IsNullOrWhiteSpace`), before it creates the command. The guard of the transaction still runs around it, so a test stays checked for an ended transaction.
- Approach 1 of the Notes, chosen by the maintainer on 2026-11-24: an empty test passes, as §8.3 says and as PostgreSQL did. Not chosen: the check in `UnitTestItemRunner` (the rule of an empty `SqlCommand` belongs to the data access of SQL Server, where `ExecuteBatchesAsync` has it), and an empty test file as an error (breaking on PostgreSQL, against §8.3).
- Tests: `SqlTestResultSetTests.Empty_test_passes` on both providers, `RunTestAsync` of a test file of 0 bytes, of only a UTF-8 BOM and of only a line break.
- Not breaking, no change of the `dblive` tables, no docs to change.

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
- 2026-11-24, start of the work: the reproduction is the one of `## Problem`, not run again. The files under `## Where` have no commits since (`git log --since=2026-11-24`), and the line numbers match.
- A file of only a UTF-8 BOM fails the same way: `AssemblyScriptSource` reads a script with `detectEncodingFromByteOrderMarks: true` (`src/DbLive/Project/AssemblyScriptSource.cs:59`), so its content is `""`. Verified on 2026-11-24 (below).
- From the code: `ExecuteQueryMultipleAsync` has one caller, `UnitTestItemRunner` (and the DA tests), always in the transaction of the test, so a test goes through `ExecuteGuardedAsync`. With an empty script that returns no result sets, the guard still runs its start and end statements and finds the transaction open: the test passes.
- Approaches considered: (1) `QueryMultipleAsync` of SQL Server returns no result sets for a text of white space, as `ExecuteBatchesAsync` skips such a batch; (2) `UnitTestItemRunner` does not call the data access for an empty test file, on both providers; (3) an empty test file fails on both providers with a message that names it: breaking on PostgreSQL, against §8.3 ("without an assert result set, the test passes"), and a file of only a comment would still pass; (4) docs only.
- Verified on 2026-11-24, SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-latest`), Microsoft.Data.SqlClient 6.1.4: `Empty_test_passes` without the fix fails for the file of 0 bytes and the file of only a BOM with `BeginExecuteReader: CommandText property has not been initialized`, and passes for the line break; with the fix all three pass. On PostgreSQL 18 with Npgsql 10.0.1 all three pass. The `SqlTest*` classes pass on both providers.
