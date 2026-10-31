---
type: feature
priority: low
created: 2026-10-31
---
# `dotnet new` template for a DbLive project

## Goal
Starting a project takes four manual steps today: packages, a `.csproj` snippet (the `ClearOutput` target, the `ProjectDir` assembly metadata, copying `Scripts`), the deployer `Program.cs`, and a test project with a fixture builder (`README.md` "Quick start", `docs/unit-testing/index.md` "Running tests in Visual Studio and CI"). A template makes the first try one command, which matters for a library that people are evaluating.

## Plan
- [ ] Package `DbLive.Templates` with a template that creates: the SQL project with `Scripts/Migrations`, `Code`, `Tests` and one example of each; the deployer; an xUnit test project with Testcontainers.
- [ ] Options: `--provider mssql|postgresql`, `--tests true|false`?
- [ ] Publish it with the other packages (0096-19).
- [ ] CI: create a project from the template and run its tests.

## Docs to update
- `README.md` ("Quick start"), `docs/gettingstarted.md`
- `docs/llms-full.txt` §2, §18.1

## Done when
- [ ] Template published and tested in CI
- [ ] Docs updated
