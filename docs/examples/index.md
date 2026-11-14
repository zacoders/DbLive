# Examples

## Walkthroughs

- [Table Split Example](table-split.md): moving a column into its own table without downtime, with versioned procedures and a temporary sync trigger.
- [Add a Stored Procedure and a Unit Test](../unit-testing/stored-proc-and-unit-test.md): the tutorial that continues Getting Started.

## Demo projects

The repository contains complete, tested projects under [`src/Demo`](https://github.com/zacoders/DbLive/tree/main/src/Demo). Use them as templates for structure, `settings.json` and test setup.

| Project | Engine | Shows |
|---------|--------|-------|
| [DemoMSSQL](https://github.com/zacoders/DbLive/tree/main/src/Demo/DemoMSSQL) | SQL Server | Migrations with breaking scripts, an item settings file for online indexes, code organised by schema with `CodeSubFoldersDeploymentOrder`, tests in `Code` and `Tests`, `setup.test.sql`, BeforeDeploy/AfterDeploy, ProjectId |
| [DemoMSSQL.Tests](https://github.com/zacoders/DbLive/tree/main/src/Demo/DemoMSSQL.Tests) | SQL Server | `[SqlFact]` and `[SqlDeployFact]` with Testcontainers; a deployment to a local server |
| [Demo.PostgreSQL.Chinook](https://github.com/zacoders/DbLive/tree/main/src/Demo/Demo.PostgreSQL.Chinook) | PostgreSQL | Timestamp versions with `EnforceTimestampVersion`, PL/pgSQL functions with tests, `rows-with-schema` assertions |
| [Demo.PostgreSQL.Chinook.Tests](https://github.com/zacoders/DbLive/tree/main/src/Demo/Demo.PostgreSQL.Chinook.Tests) | PostgreSQL | The same test setup for PostgreSQL |
| [AdventureWorks](https://github.com/zacoders/DbLive/tree/main/src/Demo/AdventureWorks) | SQL Server | A larger schema: views, triggers, functions and procedures with folder ordering; AfterDeploy scripts that set table and column descriptions and enable Query Store on every deployment |
| [DemoMSSQLEmpty](https://github.com/zacoders/DbLive/tree/main/src/Demo/DemoMSSQLEmpty) | SQL Server | A one-migration project, an older build of the database in tests: deploying it to a database deployed from DemoMSSQL fails with `DatabaseAheadOfProjectException` and changes nothing |

### Worth a look in DemoMSSQL

- [`007.migration.sql`](https://github.com/zacoders/DbLive/blob/main/src/Demo/DemoMSSQL/Scripts/Migrations/007.migration.sql), [`007.breaking.sql`](https://github.com/zacoders/DbLive/blob/main/src/Demo/DemoMSSQL/Scripts/Migrations/007.breaking.sql): expand/contract with a sync trigger; the breaking script drops the trigger and the old column and can run twice.
- [`004.migration.settings.json`](https://github.com/zacoders/DbLive/blob/main/src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.settings.json): a migration without a transaction for online index creation, with its own timeout. Its [`004.migration.sql`](https://github.com/zacoders/DbLive/blob/main/src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.sql) is re-runnable (`if not exists`), because after a failure the next deployment runs it again from the start.
- [`Code/dbo/stored-procedures`](https://github.com/zacoders/DbLive/tree/main/src/Demo/DemoMSSQL/Scripts/Code/dbo/stored-procedures): one procedure with `rows`, `has-rows` and `row-count` tests.
- [`Tests/Orders`](https://github.com/zacoders/DbLive/tree/main/src/Demo/DemoMSSQL/Scripts/Tests/Orders): shared test data in `setup.test.sql`.
