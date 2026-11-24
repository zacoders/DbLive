---
type: bug
priority: low
created: 2026-11-24
---
# SQL Server: a SQL test that cannot read a value fails with an error that names neither the value nor what to do

## Problem
DbLive reads every value of every result set of a SQL test (`MsSqlDA.ReadResultAsync`, `reader.GetValue`), so a value that SqlClient cannot read fails the test, with any assert and without one. The test fails with the error of SqlClient alone: it names neither the column nor what to do. Verified on 2026-11-24 with Microsoft.Data.SqlClient 6.1.4 and SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-latest`), a temporary test class in `DbLive.MSSQL.Tests` (removed), `select <value> as v;` without an assert:

- `geography::Point(1, 2, 4326)`, `geometry::Point(1, 2, 0)` and `hierarchyid::GetRoot()` without the `Microsoft.SqlServer.Types` package in the test project: `FileNotFoundException`, `Could not load file or assembly 'Microsoft.SqlServer.Types, Version=10.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91'. The system cannot find the file specified.` The docs name this case (§8.3, §17).
- A `decimal` that does not fit the .NET `decimal` (at most 29 digits): `cast(99999999999999999999999999999999999999 as decimal(38,0))`, `cast(0.12345678901234567890123456789012345678 as decimal(38,38))`, also inside a `sql_variant`: `OverflowException`, `Conversion overflows.` (from `SqlBuffer.get_Decimal`). The docs say nothing of it. A `decimal(38,10)` whose value fits is read.
- `json` and `vector(3)` are read.

Since 0096-166 PostgreSQL fails such a value with `Cannot read the value of the column 'p' in row 1 of result set 1, of the type public.pair: <the error of Npgsql>`, then `Select the value as text in the test.` SQL Server gives no such error, though a test project for both providers meets both.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:525` (`ReadResultAsync`, `reader.GetValue`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:478` (`ReadValue` with the catch, the same on PostgreSQL)

## Fix
Read each value under a catch, as `PostgreSqlDA` does, and fail with a `DbLiveSqlException`: `Cannot read the value of the column '<column>' in row N of result set M, of the type <type>: <the error of SqlClient>`, then `Select the value as text in the test.` The result sets counted in the order of the script, the assert result set included. `ExecuteQueryMultipleAsync` lets a `DbLiveSqlException` through as it is (`MsSqlDA.cs:464`).

- [ ] Which errors to catch: all but `SqlException`? Npgsql reads the whole row before its values; does SqlClient, without `CommandBehavior.SequentialAccess`, after `ReadAsync` too, so that `GetValue` of a `varchar(max)` of several MB does not read the network? Otherwise a broken connection in `GetValue` reads as an error of the value.
- [ ] The hint for `geography`, `geometry` and `hierarchyid`: as text is `.ToString()`, or the test project adds `Microsoft.SqlServer.Types`. Say both for a `FileNotFoundException` of that assembly, or keep the one line of PostgreSQL?
- [ ] A CLR type of the user (`create type ... external name`) without its assembly in the test project: the same `FileNotFoundException`, and is it worth a check (CLR must be enabled on the server)?

## Docs to update
- `docs/llms-full.txt` §8.3 (the bullet of `Microsoft.SqlServer.Types`; a `decimal` beyond the .NET `decimal`), §17 (the row of `Could not load file or assembly ...`, and the row of `Cannot read the value of the column ...`, now PostgreSQL only)
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [ ] Fixed, with a test: a `geography` without `Microsoft.SqlServer.Types` and a `decimal(38,0)` beyond the .NET `decimal` fail with the column and the type; `.ToString()` and `cast(... as varchar(40))` of them pass
- [ ] Docs updated

## Notes
- Found on 2026-11-24 after 0096-166: the maintainer asked for the same error on SQL Server.
