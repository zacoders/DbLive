---
type: bug
priority: medium
created: 2026-11-12
---
# DbLive.MSSQL changes the Dapper settings of the whole process, and PostgreSQL deployments then fail

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
- `src/DbLive.MSSQL/MsSqlDA.cs:46-50`
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:19-22`
- The queries that rely on them: the `DateTime` parameters of `MsSqlDA` (`applied_utc`, `created_utc`, `verified_utc`, `run_utc`, `started_utc`, `completed_utc`), and the reads into `MigrationItemRow`, `CodeItemDto` and `SchemaObjectRow` in both providers

## Fix
Change no global setting of Dapper:

- `DateTime` parameters of `MsSqlDA`: pass the type with the parameter (`DynamicParameters.Add(name, value, DbType.DateTime2)`), in one helper.
- Reads: alias the columns to the property names in the queries (`relative_path as RelativePath`), or set a type map for the DbLive row types only (`SqlMapper.SetTypeMap` per type).

## Docs to update
None: no documented behavior changes.

## Done when
- [ ] No static Dapper setting is changed by the providers
- [ ] An integration test deploys to SQL Server and then to PostgreSQL in one process
- [ ] The `datetime2(7)` columns of the `dblive` tables keep their precision (the reason for the type map)
