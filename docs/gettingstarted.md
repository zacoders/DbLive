# Getting Started


## 1. Create Project

Create a new empty `Console Application` project.

## 2. Install Packages

```bash
dotnet add package DbLive
dotnet add package DbLive.MSSQL      # or DbLive.PostgreSQL
```

## 2. Add SQL Scripts to Your Project

Add the following folder to the project
```
Scripts/
  Migrations/
  Code/
  Tests/
```

## 3. Configure Project

Add the following code to the `.csproj`; it will prepare the project and make sure all files in the `Scripts` folder will be copied to the output directory.

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

## 4. Add First Migration

Add your first migration sql script. File name format is `{timestamp}.{description}.sql`

File name: `20260808104925.create-users-table.sql`, put it to the `./Scripts/Migrations/` folder.

```sql
create table dbo.Users
(
	UserId int not null identity(1,1),
	Username nvarchar(50) not null,
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

## 6. Run app

You will see deployment process in the console app output:

```
[23:09:42 INF] Starting deployment.
[23:09:42 INF] Starting project deploy.
[23:09:43 INF] Saving migration items.
[23:09:43 INF] Deploying folder BeforeDeploy.
[23:09:43 INF] Folder BeforeDeploy is empty.
[23:09:43 INF] Deploying migrations.
[23:09:43 INF] Applied migration versions in target database: none.
[23:09:43 INF] Deploying Migrations\20260808104925.first.sql. Type Migration.
[23:09:43 INF] Deploying code.
[23:09:43 INF] Code deploy successfully completed.
[23:09:43 INF] Deploying folder AfterDeploy.
[23:09:43 INF] Folder AfterDeploy is empty.
[23:09:43 INF] Running Tests.
[23:09:43 INF] Tests Run Result> Passed: 0, Failed: 0.
[23:09:43 INF] Project deploy completed.
```


## Next steps

* [Add Stored Procedure and Unit Test](unit-testing/stored-proc-and-unit-test.md)
* [Configure Project](project/configuration.md)

See the **Demo** projects in this repository (`src/Demo`) for full examples with MSSQL and PostgreSQL.

---
