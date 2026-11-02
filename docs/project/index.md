# Project

A **DbLive project** is an ordinary .NET project that contains a `Scripts` folder with your SQL. The DbLive package embeds the scripts in the project assembly, and a small piece of C# (the *deployer*) tells DbLive which database to deploy them to.

- [Project Structure](structure.md): folders and file naming rules
- [Project Configuration](configuration.md): `settings.json`, item settings, transactions

## Layouts

**One project.** The scripts and the deployer live in one console application that references the provider package (`DbLive.MSSQL` or `DbLive.PostgreSQL`). This is the simplest option and what the demos use.

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
| `MyDb` | class library | `Scripts/`, an empty marker class; references the `DbLive` package, which embeds the scripts |
| `MyDb.Deploy` | console app | the deployer; references `MyDb` and `DbLive.MSSQL` (or `DbLive.PostgreSQL`) |
| `MyDb.Tests` | xUnit test project | SQL tests and deployment tests; references `MyDb`, the provider and `DbLive.xunit` |

The deployer and the tests find the SQL project through its assembly, for example `typeof(MyDbMarker).Assembly` where `public sealed class MyDbMarker { }` is declared in `MyDb`.

## Embedding the scripts

The `DbLive` package embeds the scripts when the project builds; there is nothing to add to the `.csproj`. The provider packages and `DbLive.xunit` bring `DbLive`, so a project that references one of them gets it. A project with only scripts (`MyDb` in the separate-projects layout) references it directly:

```bash
dotnet add package DbLive
```

A project with a `Scripts` folder gets:

- every file of the folder as a resource named after its path relative to `Scripts` (`DbLive/Migrations/001.migration.sql`). DbLive identifies scripts in the database by this path. A file like `name.fr.sql` stays in the assembly: it is not treated as a French resource and moved to a satellite assembly.
- the project folder in the assembly metadata, used only by `[SqlFact]` tests: double-clicking a SQL test in Test Explorer opens its `.sql` file.

A project without the folder gets nothing, so the deployer and the test project of the separate-projects layout do not embed anything.

Two properties in the `.csproj` of the SQL project change this:

| Property | Default | Meaning |
|----------|---------|---------|
| `DbLiveScriptsFolder` | `Scripts` | The folder to embed, relative to the project. The resource names stay relative to it, so the paths stored in the database do not depend on the folder name. |
| `DbLiveEmbedScripts` | `true` | `false` embeds nothing: for a project that has DbLive and a `Scripts` folder with other files. |

```xml
<PropertyGroup>
  <DbLiveScriptsFolder>Db</DbLiveScriptsFolder>
</PropertyGroup>
```

`SetProject` throws `ScriptsNotEmbeddedException` when the assembly has no embedded scripts, so a project that does not reference DbLive or keeps its scripts in another folder does not deploy an empty database.

A `.csproj` must not embed the scripts itself as well: with an `EmbeddedResource` for the same files, or one named `DbLive/...`, the build fails with an error that says to remove it. To embed the scripts in the `.csproj` yourself, set `<DbLiveEmbedScripts>false</DbLiveEmbedScripts>`.

!!! note "Upgrading from a version that copied the scripts to the output folder"
    Remove the DbLive block from the `.csproj`: the `ClearOutput` target, the `None` items that copy `Scripts` to the output folder, and the `ProjectDir` `AssemblyMetadata` item. The package adds what DbLive needs.

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

Both calls give DbLive the same scripts. DbLive works only with paths relative to the scripts root, such as `Migrations/001.migration.sql`, with `/` on every OS. Folders and file patterns are matched ignoring case, and logs and error messages show the real names of the files and folders. The database stores the paths in lower case, so a file renamed only by case is still the same file, and two files whose paths differ only by case stop the deployment ([details](structure.md#folders)).

## Provider files

The provider packages (`DbLive.MSSQL`, `DbLive.PostgreSQL`) contain DbLive's own SQL scripts, which create the `dblive` schema in your database. They are embedded in the provider dll, so nothing needs to be copied next to the deployer. A `DbLive.MSSQL/` or `DbLive.PostgreSQL/` folder left in `bin/` by an older package version is no longer read and can be deleted.
