---
type: feature
priority: low
created: 2026-10-31
---
# `dotnet new` template for a DbLive project

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
- [ ] Package `DbLive.Templates` with one template, `dotnet new dblive -n MyDb --provider mssql|postgresql`, that creates a solution with:
  - `MyDb`: a console app with the deployer `Program.cs` (`CreateDbIfNotExists` only with a comment that it is for a local database), `Scripts/settings.json` with `ProjectId`, and one example each in `Migrations` (with an undo script), `Code` (with a `.test.sql`) and `Tests`;
  - `MyDb.Tests`: xUnit v2, the fixture from 0096-30-02, `[SqlFact]` and `[SqlDeployFact]` classes.
- [ ] The DbLive package versions in the template are set when it is packed, so the template of release N creates a project on DbLive N.
- [ ] Packed and published with the other packages (`.github/workflows/cicd.yml`).
- [ ] CI: install the template from the packed package, create a project for each provider, and run its tests against the packages of the same build from a local feed (the feed of 0096-30-01).
- [ ] An option `--tests false`? Proposal: no; the test project is always created and can be deleted.
- [ ] Also show the template in the Visual Studio "New Project" dialog?

## Docs to update
- `README.md` ("Quick start": the template first, the manual steps after it)
- `docs/gettingstarted.md`
- `docs/llms-full.txt` §2 (packages), §18.1

## Done when
- [ ] Template published and tested in CI for both providers
- [ ] Docs updated

## Notes
2026-11-01: do after 0096-31. Done: the scripts are embedded, and `useVsProjectPath` is removed.

2026-11-03: decision of the maintainer: first reduce the setup in the packages (0096-30-01, 0096-30-02), then the template. A template helps only new projects and would freeze the boilerplate that the packages can remove for everyone.
