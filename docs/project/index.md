# Project

A **DbLive project** is an ordinary .NET project that contains a `Scripts` folder with your SQL. A few lines in the `.csproj` copy the scripts to the build output, and a small piece of C# (the *deployer*) tells DbLive which database to deploy them to.

- [Project Structure](structure.md): folders and file naming rules
- [Project Configuration](configuration.md): `settings.json`, per-migration settings, transactions

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
<!-- Remove previously copied scripts so deleted or renamed files do not linger in the output. -->
<Target Name="ClearOutput" BeforeTargets="BeforeBuild">
  <RemoveDir Directories="$(TargetDir)$(AssemblyName).Scripts" />
</Target>

<ItemGroup>
  <!-- Lets DbLive find the source folder (used by tests and by SetProject). -->
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>

  <!-- Copies Scripts\** to <output>\<AssemblyName>.Scripts\** -->
  <None Include="Scripts\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <TargetPath>$(AssemblyName).Scripts\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
  </None>
</ItemGroup>
```

!!! note
    The folder in your project must be named `Scripts`. The `ProjectDir` metadata is required: without it `SetProject` throws `ProjectDir metadata not found!`.

## How DbLive finds the scripts

`SetProject(Assembly projectAssembly, bool useVsProjectPath = false)` picks one of two locations:

| `useVsProjectPath` | Scripts are read from | Use it for |
|--------------------|-----------------------|------------|
| `false` (default) | `<current working directory>/<AssemblyName>.Scripts` | Deployers started from their output folder, `dotnet test` |
| `true` | `<project source folder>/Scripts` | Tests and local development: script edits are picked up without a rebuild |

The default resolves against the **current working directory**, not against the location of the executable. `dotnet run`, Windows services and scheduled tasks usually start in a different folder. For such deployers, register the path explicitly instead of calling `SetProject`:

```csharp
using DbLive.Common;
using Microsoft.Extensions.DependencyInjection;

string scripts = Path.Combine(AppContext.BaseDirectory, "MyDb.Scripts");

var deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()
    .SetDbConnection(connectionString)
    .ConfigureServices(s => s.AddSingleton<IProjectPath>(new ProjectPath(scripts, scripts)))
    .CreateDeployer();
```

## Provider files

The provider packages (`DbLive.MSSQL`, `DbLive.PostgreSQL`) contain DbLive's own SQL scripts, which create the `dblive` schema in your database. Their build targets copy them to `<output>/DbLive.MSSQL/` or `<output>/DbLive.PostgreSQL/` on build and on `dotnet publish`. Keep that folder next to the deployer when you copy it to a server.
