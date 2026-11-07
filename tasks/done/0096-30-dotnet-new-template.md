---
type: feature
priority: low
created: 2026-10-31
closed: 2026-11-03
---
# `dotnet new` template for a DbLive project

## Release note
**A `dotnet new` template: a database project with SQL tests in one command.** `dotnet new install DbLive.Templates`, then `dotnet new dblive -n MyDb` (`--provider postgresql` for PostgreSQL) creates a solution with two projects. `MyDb` has the scripts: a migration with its undo script, a procedure with its test, a data rule test and `settings.json` with the `ProjectId`, plus a `Program.cs` that deploys them to a local database. `MyDb.Tests` runs every SQL test file as an xUnit test and has a deployment test; `dotnet test` runs them against a database in Docker. The projects target .NET 10 and reference the DbLive packages of the template's version.

## Goal
Trying DbLive should take one command, which matters for a library that people are evaluating. Even after 0096-30-01 and 0096-30-02 the setup is spread over two pages (`README.md` "Quick start", `docs/unit-testing/index.md`). First the SQL project with `settings.json` and the deployer `Program.cs`. Then a test project with packages, a reference to the SQL project, a fixture, and the `[SqlFact]` and `[SqlDeployFact]` classes. A first try most likely fails in the test project:
- the name in `Assembly.Load("MyDb")` must match the SQL project;
- the fixture type is repeated in `[SqlFact(TestFixture = typeof(...))]`;
- Docker must be running.

SQL tests are what sets DbLive apart, so the template creates the test project.

## Depends on
- 0096-30-01: the packages embed the scripts, so the generated `.csproj` has no DbLive block.
- 0096-30-02: fixture builders in a package, so the generated fixture is one line.

## Plan
- [x] Package `DbLive.Templates` with one template, `dotnet new dblive -n MyDb --provider mssql|postgresql`, that creates a solution with:
  - `MyDb`: a console app with the deployer `Program.cs` (`CreateDbIfNotExists` only with a comment that it is for a local database), `Scripts/settings.json` with `ProjectId`, and one example each in `Migrations` (with an undo script), `Code` (with a `.test.sql`) and `Tests`;
  - `MyDb.Tests`: xUnit v2, the fixture from 0096-30-02, `[SqlFact]` and `[SqlDeployFact]` classes.
- [x] The DbLive package versions in the template are set when it is packed, so the template of release N creates a project on DbLive N.
- [x] Packed and published with the other packages (`.github/workflows/cicd.yml`).
- [x] CI: install the template from the packed package, create a project for each provider, and run its tests against the packages of the same build from a local feed (the feed of 0096-30-01).
- [x] An option `--tests false`? Proposal: no; the test project is always created and can be deleted. Decided 2026-11-03: no option.
- [x] Also show the template in the Visual Studio "New Project" dialog? Decided 2026-11-03: not in this task; `tasks/ideas/template-in-visual-studio-new-project-dialog.md`.

## Docs to update
- `README.md` ("Quick start": the template first, the manual steps after it; the NuGet list under "License and links")
- `docs/gettingstarted.md` (new section "With the template")
- `docs/llms-full.txt` §2 (packages), §3.2 (layout A), §18.1
- `docs/index.md` ("Where to start", "Packages"), `docs/llms.txt` (packages)
- `docs/unit-testing/index.md` ("Running tests in Visual Studio and CI (xUnit)")
- `docs/versioning.md` (the packages; what the policy says about the template), `Development.md` (the packages)
- `src/DbLive.Templates/README.md`, new: the package page on nuget.org

## Done when
- [x] Template published and tested in CI for both providers
- [x] Docs updated

## Notes
2026-11-01: do after 0096-31. Done: the scripts are embedded, and `useVsProjectPath` is removed.

2026-11-03: decision of the maintainer: first reduce the setup in the packages (0096-30-01, 0096-30-02), then the template. A template helps only new projects and would freeze the boilerplate that the packages can remove for everyone.

2026-11-03: decisions of the maintainer: no `--tests` option, the test project is always created. The Visual Studio "New Project" dialog is not part of this task, because CI cannot check it; it is an idea now.

2026-11-03, verified with the template engine of .NET SDK 10.0.402:
- `sourceName` puts one form of the name into all text. With `-n Contoso.Billing-Db` the files were `Contoso.Billing-Db/Contoso.Billing-Db.csproj`, but the `.slnx` pointed to `Contoso.Billing_Db/Contoso.Billing_Db.csproj` and `Assembly.Load` took `Contoso.Billing_Db`: a broken solution. The template has no `sourceName`. Derived symbols of `name` replace three tokens: `MyDb` as it is (file and folder names, paths, `Assembly.Load`, the SQL Server database name), `MyDbNamespace` in the identifier form (`Contoso.Billing_Db`), `mydb` in lower case (`ProjectId`, the PostgreSQL database name). The longer token wins, so `MyDbNamespace` is not read as `MyDb`. Without `-n` the name is the name of the output folder.
- `fileRename` of a derived symbol does not apply to the `target` of a source. The provider scripts with `"target": "MyDb/Scripts/"` landed in a folder `MyDb/Scripts/` next to the renamed project. The first run of the CI check caught it: only the deployment test ran, on a project with only `settings.json`, and passed. The provider trees repeat the project path (`scripts/mssql/MyDb/Scripts/...`) and have `"target": "./"`.
- The text of a post action is not processed by the symbols, so the instructions printed after `dotnet new` do not name the project.
- The C# extension of VS Code restores the `.csproj` files of the template folder when they are opened and creates `obj/` folders there. They are ignored by git, by the pack target and by `template.json` (`sources.exclude`).

2026-11-03, implementation decisions:
- The SQL of the two providers differs in every line, so each provider has its own tree of scripts, chosen by a `condition` on the source. `Program.cs`, the tests and the `.csproj` files use `#if (mssql)`.
- Version: the generated constant symbol `dbLiveVersion` replaces `DBLIVE_VERSION` in the `.csproj` files. Its value in the source is `0.0.0-placeholder`, the version of a local build. The target `DbLiveAddTemplates` of `DbLive.Templates.csproj` (in `BeforePack`) copies the templates to `obj/`, writes `$(Version)` into the copy of `template.json` and packs the copies into `content/`; it fails when the placeholder is missing. Only `template.json` changes, so the other files are packed as they are. The versions of xUnit, `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` are written in the template, the versions of `src/Directory.Packages.props`; the CI check shows that they work, they are updated by hand.
- The projects target `net10.0`, without a `--framework` option: .NET 10 is the LTS, and support for .NET 8 ends on 2026-11-10. The solution is a `.slnx`, which every SDK that builds `net10.0` reads.
- The first migration takes the UTC time of `dotnet new` as its version (`now` generator, `yyyyMMddHHmmss`), as `docs/gettingstarted.md` names migrations. The docs say to add later migrations with timestamps too: `002` would run before it on a new database.
- The deployer is the one of the quick start with `DeployParameters.Default with { CreateDbIfNotExists = true }` and the comment that it is for a local database only.
- The fixture class is `SqlTestFixture`: a class name with `MyDb` in it would become `Contoso.Billing-DbTestFixture`. The assembly comes from `Assembly.Load("<name>")`, as in the docs; the template writes the right name.
- The test in `Tests` checks a data rule without an assert: the insert of a user with an empty name must fail with the check constraint (SQL Server error 547, PostgreSQL `check_violation`). Verified: without the constraint the test fails with "dbo.Users accepted a user with an empty name." (PostgreSQL: "users accepted a user with an empty name"), and an undo script that does not drop the table fails the deployment test with `SchemaChangedException` ("extra table dbo.Users"), on both providers.
- The package: `PackageType` `Template`, no assembly, no dependencies, no symbols package, `NoDefaultExcludes` for `.template.config`. It is in `src/DbLive.sln`, so CI restores and builds it and `dotnet pack --no-build` works (verified).
- The CI check is a part of `.github/scripts/package-tests.cs`: it installs the template from the package into a template cache of its temporary folder (`--debug:custom-hive`), creates `Contoso.Billing-Db` (a dot and a hyphen) with the default provider and with `--provider postgresql`, checks that the DbLive package references have the version of the build, runs `dotnet test` with a TRX logger, and checks that the 3 tests ran by name and passed: the 2 SQL test files and `Contoso.Billing_Db.Tests.DeploymentTests.ReleaseDeploysAndRollsBack`.
- `docs/llms.txt` still said that the `.csproj` embeds the scripts with `LogicalName` and `WithCulture`; corrected here, after 0096-30-01.

## Result
- New project and package `src/DbLive.Templates` (`DbLive.Templates.csproj`, `README.md`) with the template `templates/dblive`: `.template.config/template.json` (short name `dblive`, option `--provider mssql|postgresql`), `MyDb.slnx`, `MyDb/` (`MyDb.csproj`, `Program.cs`, `Scripts/settings.json`), `MyDb.Tests/` (`MyDb.Tests.csproj`, `SqlTests.cs`, `DeploymentTests.cs`), and the scripts of each provider in `scripts/mssql/` and `scripts/postgresql/`. Added to `src/DbLive.sln`.
- `.github/workflows/cicd.yml`: step "Create DbLive.Templates package" into the local feed; "Publish packages" pushes it with the others. `.github/scripts/package-tests.cs`: the template check above.
- Verified locally with the seven packages packed from this branch (version `1.7.0-tmpl.1`): `package-tests.cs` passes all checks; both generated solutions run 3 tests each, all passed, with SQL Server and PostgreSQL in Docker.
- `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 505 passed, 0 failed (DbLive.Tests 397, DbLive.MSSQL.Tests 50, DbLive.PostgreSQL.Tests 37, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4), with SQL Server and PostgreSQL in Docker. `mkdocs build --strict` was not run: there is no Python on this machine.
- Docs: listed under "Docs to update"; `tasks/ideas/template-in-visual-studio-new-project-dialog.md`.
