---
type: bug
priority: medium
created: 2026-11-12
closed: 2026-11-16
---
# DbLive.MSSQL changes the Dapper settings of the whole process, and PostgreSQL deployments then fail

## Release note
None.

## Problem
The static constructor of `MsSqlDA` changes two settings of Dapper that are global for the process:

```csharp
SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
DefaultTypeMap.MatchNamesWithUnderscores = true;
```

`PostgreSqlDA` sets `MatchNamesWithUnderscores` the same way.

1. **SQL Server and PostgreSQL in one process.** After the first `MsSqlDA` is created, Dapper sends every `DateTime` parameter as `DbType.DateTime2`, also for Npgsql. Npgsql maps it to `timestamp without time zone` and rejects a UTC `DateTime`. Every DbLive deployment to PostgreSQL in that process fails.

   Verified (a temporary harness, not kept; SQL Server 2022, PostgreSQL 18): a process deploys a project to SQL Server, then another project to PostgreSQL. The PostgreSQL deployment fails with `ArgumentException: Cannot write DateTime with Kind=UTC to PostgreSQL type 'timestamp without time zone', consider using 'timestamp with time zone'`. The same PostgreSQL deployment alone in a process succeeds.

   Who meets it: a test project with fixtures for both engines, a deployer of a product that has a database on each engine, a product that supports both engines.

2. **The Dapper of the application.** DbLive runs in the process of its user: a deployer, or a test project next to the tests of the application (`DbLive.xunit`). From the code, not verified: after a DbLive deployment the queries of the application through Dapper send `DateTime` as `datetime2` and match columns with underscores to properties without them. The tests of the application then run with other Dapper settings than its production code, and a `DateTime` compared with a `datetime` column behaves differently (the precision of `datetime2`).

Both lines are in 1.5.0 too.

Found by the code review of 2026-11-12 (finding 5).

## Where
- The static constructors of `MsSqlDA` (`src/DbLive.MSSQL/MsSqlDA.cs`) and `PostgreSqlDA` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs`)
- The queries that rely on them: the `DateTime` parameters of `MsSqlDA` (`applied_utc`, `created_utc`, `verified_utc`, `run_utc`, `started_utc`, `completed_utc`), and the reads into `MigrationItemRow`, `CodeItemDto` and `SchemaObjectRow` in both providers

## Fix
Change no global setting of Dapper:

- `DateTime` parameters of `MsSqlDA`: pass the type with the parameter (`DynamicParameters.Add(name, value, DbType.DateTime2)`), in one helper.
- Reads: alias the columns to the property names in the queries (`relative_path as RelativePath`). Not `SqlMapper.SetTypeMap` for the DbLive row types: it is a global registration too (decision of 2026-11-16).

## Docs to update
None: no documented behavior changes.

## Done when
- [x] No static Dapper setting is changed by the providers
- [x] An integration test deploys to SQL Server and then to PostgreSQL in one process
- [x] The `datetime2(7)` columns of the `dblive` tables keep their precision (the reason for the type map)

## Notes
- Decision of the maintainer, 2026-11-16: no global setting of Dapper at all, also no `SqlMapper.SetTypeMap` for the internal row types of DbLive. The release note is `None.`: DbLive has no users yet, and the release is breaking anyway; an application whose Dapper relied on the settings of DbLive is too detailed for the notes.
- Without the type map Dapper sends a `DateTime` as `DbType.DateTime`, and SQL Server stores it rounded to 1/300 of a second, also in a `datetime2(7)` column. So the type map could not simply go: the parameter carries its type now.
- `DynamicParameters` with no type given still applies the type handlers of Dapper: the `XElement` of `@paths` goes as `xml`, as before.
- Dapper matches a column to a property ignoring case. PostgreSQL folds an unquoted alias to lower case (`relativepath`), and the column still maps to `RelativePath`. The `order by` of the schema snapshot names the new aliases: a `union` is ordered by the column names of its first `select`.
- A column without an alias leaves its property at the default value, without an error. The comments of `MigrationItemRow`, `CodeItemDto` and `SchemaObjectRow` say so.
- Verified: `SqlServerAndPostgreSqlInOneProcessTests` fails before the fix with the `ArgumentException` of the problem (SQL Server 2025 and PostgreSQL 18 containers). `DbLiveTablesDateTimeTests` passes with the type map, fails without it and without the helper, and passes with the helper.

## Result
- The static constructors of `MsSqlDA` and `PostgreSqlDA` are gone: DbLive changes no setting of Dapper.
- `MsSqlDA.Parameters(object)`: the parameters of a query from the properties of an object, a `DateTime` or `DateTime?` as `DbType.DateTime2`. The eight queries with a `DateTime` parameter use it: `SetSelfMigrationVersionAsync`, `SaveCodeItemAsync`, `MarkCodeAsVerifiedAsync`, `MarkMissingCodeAsRemovedAsync`, `SaveMigrationItemAsync`, `UpdateMigrationStateAsync`, `SaveUnitTestResultAsync`, `MarkItemAsAppliedAsync`. PostgreSQL needs nothing: its deployments never had the type map when the provider was alone in a process, and write UTC dates to `timestamptz`.
- Reads: the queries of `GetMigrationsAsync` and `GetCodeItemsAsync` and the schema snapshot queries of both providers name their columns after the properties (`relative_path as RelativePath`, `error as ErrorMessage`, `'Schema' as ObjectType`).
- No change to the `dblive` tables.
- Integration tests, SQL Server (`DbLive.MSSQL.Tests` references `DbLive.PostgreSQL` and `Testcontainers.PostgreSql` now): `SqlServerAndPostgreSqlInOneProcessTests` deploys a project with a migration, a code file, a SQL test and an AfterDeploy file to SQL Server and runs its tests, then does the same on PostgreSQL in a container of its own, reads the migration and code rows back by their paths, and checks that `DefaultTypeMap.MatchNamesWithUnderscores` is still `false`. `DbLiveTablesDateTimeTests` writes a date with 100 ns through every method of `MsSqlDA` that writes one and reads all twelve dates back unchanged.
- All tests pass: `DbLive.Tests` 524, `DbLive.MSSQL.Tests` 128, `DbLive.PostgreSQL.Tests` 61, the demo tests (`DemoMSSQL.Tests` 12, `AdventureWorks.Tests` 4, `Demo.PostgreSQL.Chinook.Tests` 5).
