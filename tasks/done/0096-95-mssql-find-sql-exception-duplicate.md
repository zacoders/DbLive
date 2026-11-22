---
type: chore
priority: low
created: 2026-11-17
closed: 2026-11-22
---
# `MsSqlDA.FindSqlException` duplicates `ExceptionExtensions.Get<SqlException>()`

## Release note
None. An internal cleanup of the SQL Server provider; the errors read as before.

## Problem
`MsSqlDA` has two helpers that walk the chain of inner exceptions for a `SqlException`: its private `FindSqlException`, used by `GetSqlServerError`, and `ExceptionExtensions.Get<T>()`, which the same file calls on lines 242 and 465. Both return the first `SqlException` of the chain, or `null`. A change to one of them misses the other.

The two catch blocks that call `Get<SqlException>()` then pass the exception to `GetSqlServerError`, which walks the chain again. The third caller, the catch of a `GO` batch (line 327), catches a `SqlException` and needs no walk at all.

Found by the code review of 2026-11-17 (finding 9).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:1096` (`GetSqlServerError`), `:1128-1141` (`FindSqlException`), `:242`, `:327` and `:465` (the callers)
- `src/DbLive/Common/ExceptionExtensions.cs:5-12` (`Get<TExceptionType>`)

## Fix
`GetSqlServerError` takes the `SqlException?` instead of the `Exception` and only formats it; the callers pass what they have: the result of `e.Get<SqlException>()` that they compute already, or the caught `SqlException`. `FindSqlException` is deleted. Chosen by the maintainer 2026-11-22 over calling `ex.Get<SqlException>()` inside `GetSqlServerError`, which keeps the second walk.

## Done when
- [x] `FindSqlException` deleted; `GetSqlServerError` takes `SqlException?`
- [x] The tests of `DbLive.MSSQL.Tests` pass

## Notes
- The task first argued that a change such as looking inside an `AggregateException` could reach one helper and miss the other. Neither helper looks there, and `await` unwraps an `AggregateException` anyway, so the gain is less code, not a fixed risk.
- `PostgreSqlDA.GetMessage` has a loop over `InnerException` as well, but it collects the messages of the chain; it is not a duplicate and stays.
- Tests: the first run of `DbLive.MSSQL.Tests` had one failure, `Code_file_that_outlives_the_deployment_timeout_fails_the_deployment_with_its_path`: the deployment finished without an error, so `GetSqlServerError` was not reached. The test is sensitive to the load of the machine (0096-75). The second run passed, 155 of 155.

## Result
`GetSqlServerError(SqlException? sqlEx, int? batchStartLine = null)` formats the errors of the `SqlException` it gets, or returns `""` for `null`, as before. The catch blocks of `ExecuteScriptAsync` and `ExecuteQueryMultipleAsync` pass the `SqlException` they found with `e.Get<SqlException>()`; the catch of a `GO` batch passes the caught one (`ExecuteBatchesAsync`). `FindSqlException` is deleted, and the chain is walked once per error. No behavior change, no docs. Tests: `DbLive.MSSQL.Tests` pass.
