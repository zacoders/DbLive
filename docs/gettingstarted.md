# Getting Started


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
