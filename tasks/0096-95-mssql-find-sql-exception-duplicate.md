---
type: chore
priority: low
created: 2026-11-17
---
# `MsSqlDA.FindSqlException` duplicates `ExceptionExtensions.Get<SqlException>()`

## Problem
`MsSqlDA` has two helpers that walk the chain of inner exceptions for a `SqlException`: its private `FindSqlException`, used by `GetSqlServerError`, and `ExceptionExtensions.Get<T>()`, which the same file calls on lines 223 and 401. A change to one of them (for example to look inside an `AggregateException`) misses the other.

Found by the code review of 2026-11-17 (finding 9).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:1001` (the call), `:1031-1044` (`FindSqlException`), `:223` and `:401` (`e.Get<SqlException>()`)
- `src/DbLive/Common/ExceptionExtensions.cs:5-12` (`Get<TExceptionType>`)

## Fix
Use `ex.Get<SqlException>()` in `GetSqlServerError` and delete `FindSqlException`. Both return the first `SqlException` of the chain, so nothing changes for users.

## Done when
- [ ] `FindSqlException` deleted; the tests of `DbLive.MSSQL.Tests` pass
