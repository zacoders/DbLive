---
type: bug
priority: medium
created: 2026-11-05
closed: 2026-11-07
---
# Every project with DbLive in its graph embeds its `Scripts` folder

## Release note
None.

## Problem
`buildTransitive/DbLive.props` is imported into every project that has DbLive in its dependency graph, also through a `ProjectReference`. Any such project with a `Scripts` folder silently embeds all files of the folder as `DbLive/...` resources and gets `AssemblyMetadata("ProjectDir", <absolute path of the build machine>)`. Turning it off is opt-out only (`DbLiveEmbedScripts`), and the release notes say it in one sentence (0096-30-01).

Scenario: an ASP.NET project with a classic `Scripts/` folder (jQuery and the like) references a library that references a DbLive package. After the upgrade, all its JavaScript files are embedded in its DLL as `DbLive/*.js`, the DLL grows, and the shipped assembly carries `ProjectDir` = `C:\agent\_work\...`. No warning, no error.

`ProjectDir` is used only by "go to source" of `[SqlFact]` tests, but it is in every Release build of the SQL project too.

## Where
- `src/DbLive/buildTransitive/DbLive.props:19`
- Release note of `tasks/done/0096-30-01-embed-scripts-without-csproj-block.md`

## Fix
Options for the maintainer:

- [ ] A. Ship the props in `build/` of each package (`DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL`, `DbLive.xunit`) instead of `buildTransitive/`: only a project that references a DbLive package directly embeds its scripts. Check the layouts of `docs/project/index.md` (one project, separate projects) and the template.
- [ ] B. Keep `buildTransitive/`, embed only when the project references a DbLive package directly (a condition on `@(PackageReference)`).
- [ ] C. Keep the behavior, warn when the embedded folder has files that DbLive does not read (`.js`, `.css`), and write it under breaking changes of the release.
- [ ] D. Keep `buildTransitive/`, embed only a folder with the layout of DbLive scripts: `settings.json` or a project folder (`Migrations`, `Code`, `Tests`, `BeforeDeploy`, `AfterDeploy`) in its root. Implemented, then replaced by E (Notes).
- [x] E. Keep `buildTransitive/`, embed only when the `.csproj` names the folder: `<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>`, without a default. `DbLiveEmbedScripts` is removed. Decided 2026-11-07.
- [x] `ProjectDir`: add it only when it is useful (not with `ContinuousIntegrationBuild`, or only in Debug)? Left as it is, 2026-11-07 (Notes).

## Docs to update
- `docs/llms-full.txt` §0 rule 10, §2, §3.1, §3.2, §3.4, §17, §18.1
- `docs/llms.txt`
- `docs/project/index.md` (layouts, "Embedding the scripts"), `docs/project/structure.md`
- `README.md` ("Quick start", by hand), `docs/gettingstarted.md` (step 2), `src/DbLive/README.md`
- Release notes of `tasks/done/0096-31-embed-scripts-in-assembly.md` and `tasks/done/0096-30-01-embed-scripts-without-csproj-block.md`

## Done when
- [x] A project that only has DbLive in its graph through a `ProjectReference` does not embed its `Scripts` folder, or the release note warns about it
- [x] A test of the chosen behavior (the build tests of 0096-30-01)
- [x] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96).

2026-11-07, review of the problem:
- The mechanism is real (verified in 0096-30-01: `buildTransitive/` reaches a project through a `ProjectReference`). The jQuery scenario is not: DbLive targets `net8.0` and later, classic ASP.NET with `Scripts/` is .NET Framework, and ASP.NET Core keeps static files in `wwwroot/`.
- The real case: a project that gets DbLive through a `ProjectReference` (a test project of the application that references the SQL project to deploy the database) and has its own `Scripts` folder, for example seed data of tests. When it embeds that folder itself, the build failed with "Remove the DbLive block from <project>.csproj", although the project has no DbLive block. Otherwise its files were embedded silently as `DbLive/...` with `ProjectDir`; nothing reads them.
- `buildTransitive/` is not released yet (1.5.0 does not have it), so the fix changes no released behavior. After a release, turning embedding into an opt-in would be breaking: projects would stop embedding their scripts without a build error.

2026-11-07, first decision of the maintainer: option D, with a second error message for a project without a DbLive package reference of its own. Not A or B: both lose a SQL project that gets DbLive through a wrapper (an internal package or a `ProjectReference`); its build passes and the first deployment fails with `ScriptsNotEmbeddedException`, the reason (c) was rejected in 0096-30-01. Not C: it does not fix the build error. D was implemented and verified with the package tests, on Windows and Linux.

2026-11-07, decision of the maintainer, replaces D: E, the `.csproj` names the folder. Reasons:
- No guess: a project embeds what its `.csproj` says, so no `Scripts` folder of another project is embedded, also one with a `Code` or `Tests` folder, which D embedded.
- The props stays simple. D needed a regular expression over `Directory.GetFiles` and `Directory.GetDirectories` in an MSBuild condition, with workarounds for MSBuild (verified, SDK 10.0): `\\` in an argument of a property function becomes `\`, and the array of `GetFiles` is not passed to `IsMatch` as a string.
- `DbLiveEmbedScripts` and the second error message are not needed.
- The goal of 0096-30-01 stays: the attributes that were easy to get wrong (`LogicalName`, `WithCulture="false"`) and the metadata come from the package. The `.csproj` gets one line, as the template and the demos do.
- `buildTransitive/` stays: a SQL project that gets DbLive through a wrapper still embeds its scripts when it names the folder.
- A forgotten line is an operator error: the build passes, and the first `dotnet run` or test fails with `ScriptsNotEmbeddedException`, whose message names the line. No build check is added.

2026-11-07, `ProjectDir` is left as it is: it is not new (users of 1.5.0 copied it into their `.csproj`), the PDB carries the paths of the build machine anyway, and only a project that names its scripts folder gets it.

2026-11-07, implementation of E:
- The props has no default for `DbLiveScriptsFolder`; the items need `'$(DbLiveScriptsFolder)' != ''`. The check for the folder (`Exists`) is gone: a missing folder embeds no files, and `SetProject` throws `ScriptsNotEmbeddedException`.
- The target `_DbLiveCheckScriptsEmbeddedTwice` runs only in a project that sets the property. Its message ends with "To embed the scripts in the .csproj yourself, remove <DbLiveScriptsFolder> instead." A project without the property can embed its `Scripts` folder itself without an error.
- `ScriptsNotEmbeddedException` names `<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>` first.
- The template (`MyDb.csproj`) and the four demo SQL projects set the property. `src/DbLive.Tests` keeps its own block and does not import the file.
- The release notes of 0096-31 (the upgrade from 1.5.0: remove the copy block, add the property) and 0096-30-01 (another folder name, only a project with the property embeds) are rewritten; both are not released. This task has no note of its own.

## Result
- `src/DbLive/buildTransitive/DbLive.props`: embeds the folder and adds the `ProjectDir` and `DbLiveScriptsFolder` metadata only when the `.csproj` sets `DbLiveScriptsFolder`; no default, no `DbLiveEmbedScripts`. The target `_DbLiveCheckScriptsEmbeddedTwice` runs only then.
- `ScriptsNotEmbeddedException` names the property; `DbLiveBuilderExtensionsTests.SetProject_AssemblyWithoutScripts_Throws` checks it.
- Template `MyDb.csproj` and the demos `DemoMSSQL`, `DemoMSSQLEmpty`, `Demo.PostgreSQL.Chinook`, `AdventureWorks.Database` set `<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>`; they embed the same scripts as before (38, 2, 12, 69).
- `.github/scripts/package-tests.cs`: `Provider` sets the property; new checks `NoFolderProperty` (DbLive scripts in `Scripts/` without the property: nothing embedded) and `OtherScripts` (a `ProjectReference` to the SQL project and a `Scripts` folder, with a `Migrations` folder, that the project embeds itself: it builds and embeds nothing as DbLive scripts); `OldBlock` sets the property next to the old block.
- Tests: `dotnet run .github/scripts/package-tests.cs` against packages packed from this branch: all checks passed, also the template for both providers. `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 542 passed, 0 failed (DbLive.Tests 424, DbLive.MSSQL.Tests 54, DbLive.PostgreSQL.Tests 43, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4). `mkdocs build --strict` passes.
- Docs: listed under "Docs to update".
