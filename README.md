# DbLive

**Database lifecycle management for .NET** — versioned migrations, code deployment, SQL unit tests, and safe rollback in one workflow.

DbLive treats the database as **versioned, testable, deployable code**. Use it when you want schema and SQL logic in source control, run as part of CI/CD, and roll back like any other release.

---

## Features

| Area | Description |
|------|-------------|
| **Migrations** | Versioned schema changes with optional undo scripts; ordered, repeatable deployments. |
| **Code** | Deploy stored procedures, views, functions, and other code objects separately from migrations. |
| **SQL tests** | Write tests in SQL; run them as normal C#/xUnit tests in Visual Studio or CI. |
| **Breaking changes** | Deploy breaking changes in a dedicated phase (e.g. after app rollout). |
| **Rollback** | Downgrade to a target version using undo scripts; rollback is part of the system, not a manual fix. |
| **CI/CD** | Works with GitHub Actions, Azure DevOps, TeamCity, Jenkins, or any build system that runs `dotnet`. |

---

## Quick start

### 1. Install packages

```bash
dotnet add package DbLive
dotnet add package DbLive.MSSQL      # or DbLive.PostgreSQL
dotnet add package DbLive.xunit      # for SQL unit tests
```

### 2. Add SQL scripts to your project

Organize scripts in folders (names and layout are configurable):

```
Scripts/
  Migrations/
    001.migration.users_table.sql
    002.migration.orders_table.sql
    003.migration.new_indexes.sql
  Code/
    get_user_sp.sql
    get_user_sp.test.sql
    update_order_sp.sql
    update_order_sp.test.sql
  Tests/
    admin_must_exist.test.sql
    unique_user_name.test.sql
```
### 3. Configure .csproj

Add the following code; it will prepare the project and make sure all files in the `Scripts` folder will be copied to the output directory.

```xml
<Target Name="ClearOutput" BeforeTargets="BeforeBuild">
  <RemoveDir Directories="$(TargetDir)$(AssemblyName)" />
</Target>

<ItemGroup>
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>

  <None Include="Scripts\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <TargetPath>$(AssemblyName).Scripts\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
  </None>
</ItemGroup>
```

### 4. Configure and run deployment
Microsoft SQL Server example:
```csharp
// Program.cs

using DbLive;
using DbLive.MSSQL;
using System.Reflection;

string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL;Trusted_Connection=True;";

var deployer = new DbLiveBuilder()
	.LogToConsole()
	.SqlServer()
	.SetDbConnection(dbCnnString)
	.SetProject(Assembly.GetExecutingAssembly())
	.CreateDeployer();

await deployer.DeployAsync(new DeployParameters
{
	CreateDbIfNotExists = true,
	DeployCode = true,
	DeployMigrations = true,
	RunTests = true // tests are not recommended to run on prod databases.
});
```

See the **Demo** projects in this repository (`src/Demo`) for full examples with MSSQL and PostgreSQL.

---

## Project structure

DbLive expects a clear separation of **migrations**, **code**, and **tests**. You can override folder names and patterns via `settings.json` next to your scripts.

| Folder | Purpose |
|--------|--------|
| **Migrations** | Schema and data changes in version order. Optional `*.undo.sql` per version for rollback, `*.breaking.sql` for breaking changes. |
| **Code** | Stored procedures, views, functions, etc. Paired `*.test.sql` files run as unit tests. |
| **Tests** | Standalone SQL tests (e.g. cross-object checks, data rules). |
| **BeforeDeploy** / **AfterDeploy** | Scripts run once before or after the main deployment (optional). |

Naming examples:

- Migrations: `001.migration.sql`, `002.migration.sql`, `002.undo.sql`
- Code: `GetUser.sql`, `GetUser.test.sql`
- Tests: `*.test.sql` or `t.*.sql` (patterns configurable)

---

## SQL testing

DbLive lets you **write tests in SQL** and run them as normal C# tests:

- In **Visual Studio** Test Explorer
- In **CI** (e.g. `dotnet test`)
- With the same tooling as the rest of your solution

Use SQL tests to:

- Verify stored procedures and views
- Check data rules and constraints
- Validate migration outcomes

Tests are plain SQL; a test “passes” when it runs without error and (optionally) matches expected result sets. DbLive.xunit provides the `[SqlFact]` attribute and fixture so each `.test.sql` file becomes a test case.

> SQL becomes a first-class, testable citizen in your solution.

![Visual Studio Tests](docs/visual-studio-tests.png)

---

## Deployment flow

A single `DeployAsync` run typically does (in order):

1. **Downgrade** (if requested) — apply undo scripts down to the target version
2. **BeforeDeploy** — run once-before scripts
3. **Migrations** — apply pending migrations in version order
4. **Code** — deploy or update code objects (procedures, views, etc.)
5. **Breaking changes** — apply breaking migration steps (if enabled)
6. **AfterDeploy** — run once-after scripts
7. **Unit tests** — run all SQL tests (unless disabled)

You control what runs via `DeployParameters` (e.g. `DeployMigrations`, `DeployCode`, `RunTests`, `DeployBreaking`, `AllowDatabaseDowngrade`). Presets like `DeployParameters.Default` and `DeployParameters.Breaking` are available.

### Typical CI/CD pipeline

DbLive fits into a release pipeline like this:

1. **Build** — compile the solution
2. **Deploy database to Docker** — in a Docker database container (e.g. Testcontainers)
3. **Run tests** — `dotnet test`. It will run both C# and SQL tests.
4. **Deploy database** — migrations and code (DbLive)
5. **Deploy application** — new app version
6. **Drop old application** — remove previous app (if needed)
7. **Deploy breaking changes** — run DbLive with `DeployParameters.Breaking` when the old app is gone

---

## Supported databases

| Engine | Package | Status |
|--------|---------|--------|
| Microsoft SQL Server / Azure SQL | `DbLive.MSSQL` | Supported |
| PostgreSQL | `DbLive.PostgreSQL` | Supported |
| SQLite | — | Planned |
| MySQL | — | Planned |

---

## Design goals

- **Database is not a special snowflake** — it is code: versioned in repo, reviewed, and deployed like application code.
- **SQL is testable** — procedures, views, and migrations can have automated tests.
- **Deployments are predictable** — same order, same scripts, same outcome in dev and prod.
- **Deployments are reversible** — rollback is built-in via undo scripts, not a manual emergency fix.
- **Rollback is part of the system** — downgrade to a target version is a normal operation, not an exception.

---

## Demo and examples

This repository includes working demos under **`src/Demo`**:

- **DemoMSSQL** — MSSQL with migrations, code, tests, BeforeDeploy/AfterDeploy, and breaking changes
- **DemoMSSQL.Tests** — xUnit + `[SqlFact]` + Testcontainers
- **Demo.PostgreSQL.Chinook** — PostgreSQL sample (Chinook demo database)
- **Demo.PostgreSQL.Chinook.Tests** — PostgreSQL tests with Docker
- **AdventureWorks.Database** / **AdventureWorks.Tests** — AdventureWorks demo

Use them as templates for structure, `settings.json`, and test setup.

---

## License and links

- **License:** [MIT](https://opensource.org/licenses/MIT)
- **Repository:** [github.com/zacoders/DbLive](https://github.com/zacoders/DbLive)
- **NuGet:** DbLive, DbLive.MSSQL, DbLive.PostgreSQL, DbLive.xunit
