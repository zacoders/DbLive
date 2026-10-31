---
type: feature
priority: medium
created: 2026-11-01
---
# Embed the scripts in the SQL project assembly

## Goal
Today the `.csproj` copies `Scripts/` to `<output>/<AssemblyName>.Scripts/`, and DbLive reads the scripts from there. The copy causes problems and gives nothing back: people review scripts in the repository, not in `bin/`, and what was applied is stored in the database (`Content` of migration items).

Problems with the copy:
- **Stale scripts.** A deleted or renamed script stays in `bin/` and is deployed when the list of written files in `obj/` is lost but `bin/` is kept (verified, see Notes). The `ClearOutput` target that should prevent this removes the wrong folder in all four demos (0096-10), and even the fixed target does not clean projects that reference the SQL project (verified).
- **A build block every user copies by hand** (`ClearOutput`, `None` items with `TargetPath`).
- **The copy flows through `ProjectReference`** into the outputs of test and deployer projects.
- **Scripts in `bin/` can be edited on a server** before deployment, out of sync with git.

Embedded scripts make one immutable artifact: what was built is what gets deployed. There is nothing to clean, and publish, single-file and Docker need no special handling.

The maintainer is the only user so far, so the API may break. Existing databases must keep deploying with no changes (see "Compatibility").

## Design
The SQL project `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />

  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

| Call | Scripts are read from | "Go to source" in Test Explorer |
| --- | --- | --- |
| `SetProject(assembly)` | resources of `assembly` with the `DbLive/` prefix | `<ProjectDir>/Scripts/<RelativePath>`; without `ProjectDir`, no navigation |
| `SetScriptsFolder(path)` | the folder | `<path>/<RelativePath>` |

- `useVsProjectPath` is removed. Test projects reference the SQL project, so they read the same scripts from its dll, deploy and run SQL tests. Its documented advantage, "edits are picked up without a rebuild", does not matter: Test Explorer and `dotnet test` build before running.
- `ProjectDir` stays, only for "Go to source" of `[SqlFact]` tests: `SqlFactDiscoverer` builds the full path of the `.sql` file, and `SqlXunitTestCase` puts it into `SourceInformation`, so a double click in Test Explorer opens the `.sql` file. This works today and is used. `ProjectDir` stays in every configuration, because tests may run in Release.

## Compatibility
DbLive identifies scripts in the database by `RelativePath` (`Migrations/001.migration.sql`; code items are found by `FindCodeItemAsync(RelativePath)`) and by content hash. For the same project, the embedded file system must return the same relative paths and the same text as the folder. Otherwise existing databases see changed or new scripts: a checksum mismatch for migrations, a redeployment for code. Default resource names (`DemoMSSQL.Scripts.Migrations.001.migration.sql`) lose the folder structure, so `LogicalName` is required.

## Where
- `src/DbLive/DbLiveBuilderExtensions.cs:40-62`: `SetProject`, `GetVisualStudioProjectPath`.
- `src/DbLive/Common/IProjectPath.cs`: `IProjectPath`, `ProjectPath`.
- `src/DbLive/Common/IFileSystem.cs`, `src/DbLive/Common/FileSystem.cs`: path based; used by `DbLiveProject` and `SettingsAccessor` (`src/DbLive/Project/ProjectSettingsReader.cs`).
- `src/DbLive/Bootstrapper.cs:23`: one `IFileSystem` singleton serves both the user project and `InternalDbLiveProject` (DbLive's own scripts).
- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:70-78`, `src/DbLive.xunit/SqlTest/SqlXunitTestCase.cs:39`: "Go to source".
- Demos: `DemoMSSQL`, `DemoMSSQLEmpty`, `Demo.PostgreSQL.Chinook`, `AdventureWorks.Database` (`.csproj`); fixture builders with `useVsProjectPath: true` in `DemoMSSQL.Tests`, `Demo.PostgreSQL.Chinook.Tests`, `AdventureWorks.Tests`.
- Tests: `src/DbLive.Tests/Builder/DbLiveBuilderExtensionsTests.cs`, `src/DbLive.Tests/Builder/DbLiveBuilderTests.cs`, `src/DbLive.Tests.Common/TempProject.cs`, `src/DbLive.Tests.Common/MockSet.cs`.

## Plan
- [ ] `EmbeddedFileSystem : IFileSystem` over the resources with the `DbLive/` prefix, so `DbLiveProject` stays as it is.
  - Normalize `\` to `/` in resource names: `%(RecursiveDir)` uses `\` when the project is built on Windows.
  - Return paths under a virtual root, joined with `Path.DirectorySeparatorChar` like the physical file system: `DbLiveProject.cs:51` matches subfolders by `subPath + Path.DirectorySeparatorChar`, and `DbLiveProject.cs:267` sorts BeforeDeploy/AfterDeploy items by full path.
  - Match patterns (`*.sql`, `*.test.sql`, exclude patterns) with `FileSystemName.MatchesSimpleExpression`, the matcher behind `Directory.EnumerateFiles`. Derive directories from file paths.
  - Read with `StreamReader` and BOM detection, as `File.ReadAllTextAsync` does, so content hashes do not change.
- [ ] Separate the project's file system from DbLive's own. For example, `IProjectPath` gets `IFileSystem FileSystem`, and `DbLiveProject` and `SettingsAccessor` take it from there; `InternalDbLiveProject` keeps the physical `FileSystem`.
- [ ] `SetProject(assembly)` reads embedded scripts. When the assembly has no `DbLive/` resources, it throws an exception that names the assembly and shows the `EmbeddedResource` line, instead of deploying an empty project.
- [ ] `SetScriptsFolder(path)` uses the physical file system on that folder. It replaces registering `IProjectPath` by hand (`docs/llms-full.txt` §3.4).
- [ ] Remove `useVsProjectPath`.
- [ ] `[SqlFact]` discovery without `ProjectDir`: leave `SourceInformation` empty instead of throwing "ProjectDir metadata not found!".
- [ ] Demos: the new `.csproj` block; fixture builders call `SetProject(assembly)`.
- [ ] Check with an experiment that `WithCulture="false"` keeps `name.fr.sql` in the main assembly. Without it, MSBuild treats such a file as a French resource and moves it to a satellite assembly: the script silently disappears.
- [ ] Is `<None Remove="Scripts\**\*" />` needed so that Visual Studio does not list the files twice?
- [ ] Virtual root: which string? It appears in `FileData.FilePath`, in logs and in exception messages.
- [ ] `SetScriptsFolder` with a relative path: resolve it against the current directory (expected for a CLI argument)?

### Tests
- [ ] Parity test: load each demo project from its dll and from its source folder, then compare `RelativePath` and `ContentHash` of all migrations, code items, tests, BeforeDeploy/AfterDeploy items, and the settings. Equal lists mean existing databases will not notice the change.
- [ ] Unit tests for `EmbeddedFileSystem`: patterns, exclude patterns, subfolders, directories, a file with a BOM, `\` in resource names.
- [ ] `SetProject` throws when the assembly has no scripts; `[SqlFact]` discovery works without `ProjectDir`.
- [ ] Demo deployment tests and `[SqlFact]` tests pass with scripts read from the dll.

## Docs to update
- `README.md` ("3. Configure .csproj")
- `docs/gettingstarted.md` (`.csproj` block; step 7, "reads the scripts from `<AssemblyName>.Scripts`")
- `docs/project/index.md` (`.csproj` block; "How DbLive finds the scripts")
- `docs/deployment/index.md:34`
- `docs/unit-testing/index.md` (fixture builder; "New test files appear after a build")
- `docs/unit-testing/stored-proc-and-unit-test.md` (fixture builder; step 4, "without a rebuild")
- `docs/llms.txt:8`
- `docs/llms-full.txt`: rule 10 in §0; §3.1 (new block; remove the `ClearOutput` note and the `ProjectReference` paragraph); §3.3; §3.4; §11.1 (`SetProject`, `SetScriptsFolder`); §13.1; §13.2 (discovery reads from the dll); §17 ("ProjectDir metadata not found!", the new "no scripts embedded" exception); §18.1; §18.11; §19.

## Out of scope
- **DbLive's own scripts.** `DbLive.MSSQL` and `DbLive.PostgreSQL` copy their scripts into the user's `bin/` with `buildTransitive` targets (`src/DbLive.MSSQL/DbLive.MSSQL.targets`, `src/DbLive.PostgreSQL/DbLive.PostgreSQL.targets`). `EmbeddedFileSystem` can serve them too, and then the targets can go. This is a separate task after this one. Not verified: the custom `Copy` there does not add to `FileWrites`, so a script renamed in a newer package version may stay in `bin/` and be deployed by the self-deployer.
- **Keeping the path out of the production dll.** `ProjectDir` could move into the test project via targets in `DbLive.xunit`. This is only needed for reproducible builds in CI.
- **A standalone deployer (CLI)** that deploys from a folder. It will use `SetScriptsFolder`.

## Done when
- [ ] `SetProject(assembly)` reads embedded scripts, `SetScriptsFolder(path)` reads a folder, and `useVsProjectPath` is removed
- [ ] The parity test passes for all demos
- [ ] Demos and docs use the new `.csproj` block; no `ClearOutput` is left in code, demos or docs
- [ ] "Go to source" still opens the `.sql` file (check in Visual Studio)

## Notes
2026-11-01, decided with the maintainer while reviewing 0096-10:
- Copying the scripts to `bin/` is not a feature: nobody looks at them there. Scripts belong in the dll by default; a folder is an explicit option (`SetScriptsFolder`), mainly for a future CLI.
- Keep `ProjectDir` for "Go to source", in every configuration.

Experiments on a copy of `DemoMSSQLEmpty`, .NET SDK 10.0.402:
- In a normal incremental build, the `IncrementalClean` target deletes a removed script from `bin/` (it tracks written files in `obj/*.FileListAbsolute.txt`), both in the SQL project and in a project that references it.
- With `obj/` deleted and `bin/` kept, the removed script stays in `bin/` with the demos' `ClearOutput`, and is deleted with `$(TargetDir)$(AssemblyName).Scripts`. In a referencing project, it stays with both.
- A dll built without `ProjectDir` already contains the absolute path of its `.pdb`, in Release too and with `ContinuousIntegrationBuild=true` in a folder without git. So `ProjectDir` is not what puts a build path into the dll.
