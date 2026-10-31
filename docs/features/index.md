# Features

| Feature | Summary | Details |
|---------|---------|---------|
| **Versioned migrations** | Each version is applied once per database, in ascending order; each migration, breaking and undo script in its own transaction. | [Migrations](../deployment/migrations.md) |
| **Undo scripts and downgrade** | Undo scripts are stored in the database. Deploying an older build with `AllowDatabaseDowngrade` reverts newer versions, newest first, each in its own transaction. | [Downgrade](../deployment/migrations.md#downgrade) |
| **Breaking changes phase** | Destructive scripts run only in a separate deployment, after the old application is gone. | [Breaking Changes](../deployment/breaking-changes.md) |
| **Re-runnable code** | Procedures, functions and views are redeployed only when their file changes; parallel, with dependency retries. | [Code Deployment](../deployment/code.md) |
| **SQL unit tests** | `.sql` tests with result-set assertions, run in rolled-back transactions, at deployment time or from xUnit. | [Unit Testing](../unit-testing/index.md) |
| **Deployment tests** | `[SqlDeployFact]` deploys to an empty database six ways, including undo round-trips. | [Deployment tests](../unit-testing/index.md#deployment-tests) |
| **Checksum validation** | Applied migration files cannot change unnoticed; `Strict` or `Warn`, with an explicit repair option. | [Checksums](../deployment/migrations.md#checksums) |
| **ProjectId** | Binds a project to a database and blocks deployments to the wrong one. | [ProjectId](project-id.md) |
| **Timestamp versions** | `EnforceTimestampVersion` requires `yyyyMMddHHmmss` versions, so team branches do not collide. | [Version numbers](../deployment/migrations.md#version-numbers) |
| **Item settings** | Transaction level, isolation level and timeout per migration, breaking or undo script, for example for online index builds. | [Item settings](../project/configuration.md#item-settings) |
| **BeforeDeploy / AfterDeploy** | Idempotent scripts that run around every deployment. | [Project Structure](../project/structure.md#beforedeploy-and-afterdeploy) |
| **Two engines** | SQL Server / Azure SQL (`DbLive.MSSQL`) and PostgreSQL (`DbLive.PostgreSQL`). SQLite and MySQL are planned. | [Project](../project/index.md) |
