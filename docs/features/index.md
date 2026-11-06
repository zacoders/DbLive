# Features

| Feature | Summary | Details |
|---------|---------|---------|
| **Versioned migrations** | Each version is applied once per database, in ascending order; each migration and breaking script in its own transaction. | [Migrations](../deployment/migrations.md) |
| **Older build protection** | A build that lacks versions applied to the database fails with `DatabaseAheadOfProjectException` before it changes anything. DbLive does not roll back a database: before its breaking phase a release is rolled back by rolling back the application. | [Roll back a release](../deployment/migrations.md#roll-back-a-release) |
| **Breaking changes phase** | Destructive scripts run only in a separate deployment, after the old application is gone. | [Breaking Changes](../deployment/breaking-changes.md) |
| **Re-runnable code** | Procedures, functions and views are redeployed only when their file changes; parallel, with dependency retries. | [Code Deployment](../deployment/code.md) |
| **SQL unit tests** | `.sql` tests with result-set assertions, run in rolled-back transactions as xUnit tests against a temporary database. A deployment never runs them. | [Unit Testing](../unit-testing/index.md) |
| **Deployment tests** | `[SqlDeployFact]` deploys to an empty database the way production does (release, breaking phase, breaking again), runs every breaking script a second time, and checks that the second run does not change the schema. | [Deployment tests](../unit-testing/deployment-tests.md) |
| **Checksum validation** | Applied migration files cannot change unnoticed; `Strict` or `Warn`, with an explicit repair option. | [Checksums](../deployment/migrations.md#checksums) |
| **ProjectId** | Binds a project to a database and blocks deployments to the wrong one. | [ProjectId](project-id.md) |
| **Timestamp versions** | `EnforceTimestampVersion` requires `yyyyMMddHHmmss` versions, so team branches do not collide. | [Version numbers](../deployment/migrations.md#version-numbers) |
| **Item settings** | Transaction level, isolation level and timeout per migration or breaking script, for example for online index builds. | [Item settings](../project/configuration.md#item-settings) |
| **BeforeDeploy / AfterDeploy** | Idempotent scripts that run around every deployment. | [Project Structure](../project/structure.md#beforedeploy-and-afterdeploy) |
| **Two engines** | SQL Server / Azure SQL (`DbLive.MSSQL`) and PostgreSQL (`DbLive.PostgreSQL`). SQLite and MySQL are planned. | [Project](../project/index.md) |
