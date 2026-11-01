# Project

A **DbLive project** is an ordinary .NET project that contains a `Scripts` folder with your SQL. A few lines in the `.csproj` embed the scripts in the project assembly, and a small piece of C# (the *deployer*) tells DbLive which database to deploy them to.

- [Project Structure](structure.md): folders and file naming rules
- [Project Configuration](configuration.md): `settings.json`, item settings, transactions

## Layouts

**One project.** The scripts and the deployer live in one console application. This is the simplest option and what the demos use.

```
MyDb/
  MyDb.csproj
  Program.cs          <- deployer
  Scripts/
    settings.json     <- optional
    Migrations/
    Code/
    Tests/
```

**Separate projects.** Recommended once the solution grows:

| Project | Type | Contains |
|---------|------|----------|
| `MyDb` | class library | `Scripts/`, the `.csproj` block below, an empty marker class |
| `MyDb.Deploy` | console app | the deployer; references `MyDb` and `DbLive.MSSQL` (or `DbLive.PostgreSQL`) |
| `MyDb.Tests` | xUnit test project | SQL tests and deployment tests; references `MyDb`, the provider and `DbLive.xunit` |

The deployer and the tests find the SQL project through its assembly, for example `typeof(MyDbMarker).Assembly` where `public sealed class MyDbMarker { }` is declared in `MyDb`.

## The `.csproj` block

Add this to the project that contains the `Scripts` folder:

```xml
<ItemGroup>
  <!-- Embeds Scripts\** in the assembly; SetProject(assembly) reads the scripts from there. -->
  <EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />

  <!-- Lets "Go to source" of SQL tests in Test Explorer open the .sql file. -->
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

What each part does:

- `LogicalName` keeps the folder structure: DbLive identifies scripts in the database by their path relative to `Scripts` (`Migrations/001.migration.sql`), so it must be the same as in the folder.
- `WithCulture="false"` keeps a file like `name.fr.sql` in the assembly. Without it, MSBuild treats the file as a French resource, moves it to a satellite assembly, and the script is not deployed.
- `ProjectDir` is used only by `[SqlFact]` tests: double-clicking a SQL test in Test Explorer opens its `.sql` file. Without it, the tests still run, but "Go to source" does not open the `.sql` file.

!!! note
    The folder in your project must be named `Scripts`. `SetProject` throws `ScriptsNotEmbeddedException` when the assembly has no embedded scripts, so a missing block does not deploy an empty project.

## How DbLive finds the scripts

| Call | Scripts are read from | Use it for |
|------|-----------------------|------------|
| `SetProject(Assembly projectAssembly)` | the resources embedded in `projectAssembly` | Deployers and tests |
| `SetScriptsFolder(string path)` | the folder; a relative path is resolved against the current directory | Scripts that are not built into an assembly |

With `SetProject` there is nothing to copy or clean: your scripts travel inside the dll, also through `dotnet publish` (DbLive's own scripts travel inside the provider dll, see [Provider files](#provider-files)), and a script deleted from the project is gone from the next build. The deployer does not depend on the current working directory. Because the scripts are part of the build, change a script and build before you deploy; Visual Studio, `dotnet run` and `dotnet test` build first.

Test projects reference the SQL project, so they read the same scripts from its dll.

To read scripts from a folder instead:

```csharp
var deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()
    .SetDbConnection(connectionString)
    .SetScriptsFolder(@"D:\Deploy\MyDb\Scripts")
    .CreateDeployer();
```

The folder is read again on every deployment, so files added or deleted after `SetScriptsFolder` are picked up.

Both calls give DbLive the same scripts. DbLive works only with paths relative to the scripts root, such as `Migrations/001.migration.sql`, with `/` on every OS: they are stored in the database and shown in logs and error messages. Folders and file patterns are matched ignoring case, and a relative path keeps the real names of the files and folders.

## Provider files

The provider packages (`DbLive.MSSQL`, `DbLive.PostgreSQL`) contain DbLive's own SQL scripts, which create the `dblive` schema in your database. They are embedded in the provider dll, so nothing needs to be copied next to the deployer. A `DbLive.MSSQL/` or `DbLive.PostgreSQL/` folder left in `bin/` by an older package version is no longer read and can be deleted.
