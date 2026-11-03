# Getting Started

There are two ways to start. The `dotnet new` template creates a working solution with SQL tests in one command. The steps after it build the same SQL project by hand and explain each part; follow them to add DbLive to an existing solution.

## With the template

```bash
dotnet new install DbLive.Templates
dotnet new dblive -n MyDb --provider mssql      # or --provider postgresql
cd MyDb
```

`-n` names the solution and the SQL project (default: the name of the current folder); the test project is `MyDb.Tests`. The template creates:

| File | What it is |
|------|------------|
| `MyDb/Scripts/settings.json` | The [ProjectId](features/project-id.md) of the project, the name in lower case |
| `MyDb/Scripts/Migrations/<timestamp>.users.sql` | The first migration: a users table. The version is the UTC time when the template ran |
| `MyDb/Scripts/Migrations/<timestamp>.undo.sql` | Its undo script |
| `MyDb/Scripts/Code/dbo.GetUser.sql`, `dbo.GetUser.test.sql` | A procedure and its test (PostgreSQL: `get_user.sql`, a function) |
| `MyDb/Scripts/Tests/user-name-is-required.sql` | A test of a data rule, without an assert: it fails when the script raises an error |
| `MyDb/Program.cs` | The deployer: deploys the scripts to a local database and creates it if it does not exist |
| `MyDb.Tests/SqlTests.cs` | The fixture and `[SqlFact]`: every SQL test file is a test case of its own |
| `MyDb.Tests/DeploymentTests.cs` | `[SqlDeployFact]`: deploys the project as production does and runs the undo script ([Deployment tests](unit-testing/deployment-tests.md)) |

Run the tests, in Visual Studio Test Explorer or with:

```bash
dotnet test
```

The fixtures start SQL Server or PostgreSQL in Docker (Testcontainers), so Docker must be running; every test class gets a new database, which is dropped after the tests. Without Docker, point the fixtures at a server you run ([A fixture builder](unit-testing/index.md#1-a-fixture-builder)).

To deploy to a local database, check the connection string in `MyDb/Program.cs` and run:

```bash
dotnet run --project MyDb
```

The projects target .NET 10 and reference the DbLive packages of the same version as the template. `dotnet new install DbLive.Templates` updates the template to the latest version.

The example scripts are there to show the folders; replace them with yours. A migration can be changed only until it is deployed to a database that you keep ([Rules of thumb](deployment/migrations.md#rules-of-thumb)): the tests deploy to databases they drop, so they do not count. Name new migrations with a timestamp too, as in step 4 below: a lower version such as `002` would run before the example migration on a new database.

## 1. Create Project

Create a console application named `MyDb` (in Visual Studio: Console App):

```bash
dotnet new console -n MyDb
cd MyDb
```

## 2. Install Packages

```bash
dotnet add package DbLive.MSSQL      # or DbLive.PostgreSQL; brings the DbLive package
```

The DbLive package embeds all files in the `Scripts` folder in the project assembly when the project builds. There is nothing to add to the `.csproj` (see [Embedding the scripts](project/index.md#embedding-the-scripts)).

## 3. Add SQL Scripts to Your Project

Add the following folder to the project
```
Scripts/
  Migrations/
  Code/
  Tests/
  settings.json
```

All three folders are optional; a project can, for example, contain only code. See [Project Structure](project/structure.md).

`settings.json` names the project. The first deployment stores the name in the database, and a deployer of another project then cannot deploy into it (see [ProjectId](features/project-id.md)). Without it every deployment logs a warning.

```json
{
  "ProjectId": "my-app-db"
}
```

## 4. Add First Migration

Add your first migration sql script. File name format is `{timestamp}.{description}.sql`

File name: `20260808104925.create-users-table.sql`, put it to the `./Scripts/Migrations/` folder.

```sql
create table dbo.Users
(
	UserId int not null identity(1,1),
	Name nvarchar(50) not null,
	constraint PK_Users primary key ( UserId )
);
```

## 5. Configure and Run Deployment

Copy the following code to the `Program.cs`

```csharp
// Program.cs

using DbLive;
using DbLive.MSSQL;
using System.Reflection;

string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL;Trusted_Connection=True;";

var builder = new DbLiveBuilder()
	.LogToConsole()
	.SqlServer()
	.SetDbConnection(dbCnnString)
	.SetProject(Assembly.GetExecutingAssembly());

await builder.CreateDeployer().DeployAsync(new DeployParameters
{
	CreateDbIfNotExists = true, // a local database; off by default, keep it off in production
	DeployCode = true,
	DeployMigrations = true
});
```

If the connection fails with "The certificate chain was issued by an authority that is not trusted", the local SQL Server uses a self-signed certificate, which Microsoft.Data.SqlClient rejects by default. For a local server, add `TrustServerCertificate=True;` to the connection string.

## 6. Run App

Run the app from Visual Studio or with `dotnet run`. DbLive reads the scripts embedded in the assembly, so the build has to run after you change a script; Visual Studio and `dotnet run` build first (see [How DbLive finds the scripts](project/index.md#how-dblive-finds-the-scripts)).

You will see deployment process in the console app output:

```
[23:09:42 INF] Starting deployment.
[23:09:42 INF] Starting project deploy from MyDb.dll.
[23:09:42 INF] ProjectId bound to database: my-app-db.
[23:09:43 INF] Saving migration items.
[23:09:43 INF] Deploying folder BeforeDeploy.
[23:09:43 INF] Folder BeforeDeploy is empty.
[23:09:43 INF] Deploying migrations.
[23:09:43 INF] Applied migration versions in target database: none.
[23:09:43 INF] Deploying Migrations/20260808104925.create-users-table.sql. Type Migration. Transaction Migration, isolation level ReadCommitted, timeout 12:00:00.
[23:09:43 INF] Deploying code.
[23:09:43 INF] Code deploy successfully completed.
[23:09:43 INF] Deploying folder AfterDeploy.
[23:09:43 INF] Folder AfterDeploy is empty.
[23:09:43 INF] Project deploy completed.
```

A deployment does not run SQL tests. To write a test and run it, continue with [Add Stored Procedure and Unit Test](unit-testing/stored-proc-and-unit-test.md).


## Next steps

* [Add Stored Procedure and Unit Test](unit-testing/stored-proc-and-unit-test.md)
* [Configure Project](project/configuration.md)
* [Database as a Service](database-as-service.md): what to do beyond classic migrations, for releases without downtime

See the **Demo** projects in this repository (`src/Demo`) for full examples with MSSQL and PostgreSQL.

---
