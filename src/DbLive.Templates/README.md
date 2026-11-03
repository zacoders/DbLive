# DbLive.Templates

A `dotnet new` template for [DbLive](https://github.com/zacoders/DbLive): a database project with migrations, code and SQL tests, deployed by a small console app and tested with xUnit against a database in Docker.

```bash
dotnet new install DbLive.Templates
dotnet new dblive -n MyDb --provider mssql      # or --provider postgresql
cd MyDb
dotnet test                                     # Docker must be running
```

The template creates a solution with two projects:

- `MyDb`: a console app. `Scripts/` holds the SQL: `settings.json` with the `ProjectId`, a migration with its undo script in `Migrations`, a procedure (a function on PostgreSQL) with its test in `Code`, and a data rule test in `Tests`. `Program.cs` deploys them to a local database: `dotnet run --project MyDb`.
- `MyDb.Tests`: an xUnit v2 project. Every SQL test file is a test case of its own (`[SqlFact]`), and a deployment test (`[SqlDeployFact]`) deploys the project as production does and runs every undo script. The database server runs in Docker (Testcontainers).

| Option | Values |
|---|---|
| `-n`, `--name` | The name of the solution and of the SQL project; the test project is `<name>.Tests`. Default: the name of the output folder. |
| `--provider` | `mssql` (SQL Server, default) or `postgresql`. |

The projects target .NET 10 and reference the DbLive packages of the same version as the template: update the template (`dotnet new install DbLive.Templates`) to create projects on the latest DbLive.

- Getting started: https://zacoders.github.io/DbLive/gettingstarted/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
