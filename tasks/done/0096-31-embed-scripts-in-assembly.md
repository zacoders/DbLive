---
type: feature
priority: medium
created: 2026-11-01
closed: 2026-11-01
breaking: true
---
# Embed the scripts in the SQL project assembly

## Release note
**Scripts are embedded in the SQL project assembly.** `SetProject(assembly)` reads the scripts from the resources of the assembly instead of a copy in the output folder: a deleted or renamed script no longer stays in `bin/` to be deployed, and the working directory no longer matters. `SetScriptsFolder(path)` reads a folder instead, for example in a deployer that gets the scripts as an argument. To upgrade, replace the part of the SQL project's `.csproj` that copies `Scripts` to the output (the `None` items with `TargetPath` and the `ClearOutput` target) with the block below.

```xml
<ItemGroup>
  <EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />

  <!-- Lets "Go to source" of SQL tests in Test Explorer open the .sql file. -->
  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

`SetProject` fails with `ScriptsNotEmbeddedException` when the assembly has no embedded scripts. The `useVsProjectPath` argument of `SetProject` is removed: a test project reads the scripts from the dll of the SQL project it references. Existing databases deploy as before: relative paths and checksums do not change.

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
- [x] `EmbeddedFileSystem : IFileSystem` over the resources with the `DbLive/` prefix, so `DbLiveProject` stays as it is.
  - Normalize `\` to `/` in resource names: `%(RecursiveDir)` uses `\` when the project is built on Windows.
  - Return paths under a virtual root, joined with `Path.DirectorySeparatorChar` like the physical file system: `DbLiveProject.cs:51` matches subfolders by `subPath + Path.DirectorySeparatorChar`, and `DbLiveProject.cs:267` sorts BeforeDeploy/AfterDeploy items by full path.
  - Match patterns (`*.sql`, `*.test.sql`, exclude patterns) with `FileSystemName.MatchesSimpleExpression`, the matcher behind `Directory.EnumerateFiles`. Derive directories from file paths. Done with `MatchesWin32Expression`, see Notes.
  - Read with `StreamReader` and BOM detection, as `File.ReadAllTextAsync` does, so content hashes do not change.
- [x] Separate the project's file system from DbLive's own. For example, `IProjectPath` gets `IFileSystem FileSystem`, and `DbLiveProject` and `SettingsAccessor` take it from there; `InternalDbLiveProject` keeps the physical `FileSystem`.
- [x] `SetProject(assembly)` reads embedded scripts. When the assembly has no `DbLive/` resources, it throws an exception that names the assembly and shows the `EmbeddedResource` line, instead of deploying an empty project. (`ScriptsNotEmbeddedException`)
- [x] `SetScriptsFolder(path)` uses the physical file system on that folder. It replaces registering `IProjectPath` by hand (`docs/llms-full.txt` §3.4).
- [x] Remove `useVsProjectPath`.
- [x] `[SqlFact]` discovery without `ProjectDir`: leave `SourceInformation` empty instead of throwing "ProjectDir metadata not found!".
- [x] Demos: the new `.csproj` block; fixture builders call `SetProject(assembly)`.
- [x] Check with an experiment that `WithCulture="false"` keeps `name.fr.sql` in the main assembly. Without it, MSBuild treats such a file as a French resource and moves it to a satellite assembly: the script silently disappears. Verified, see Notes; also covered by `EmbeddedFileSystemTests.FromAssembly_KeepsCultureNamedScriptsInMainAssembly`.
- [x] Is `<None Remove="Scripts\**\*" />` needed so that Visual Studio does not list the files twice? No, see Notes.
- [x] Virtual root: which string? It appears in `FileData.FilePath`, in logs and in exception messages. `<AssemblyName>.dll`, see Notes.
- [x] `SetScriptsFolder` with a relative path: resolve it against the current directory (expected for a CLI argument)? Yes.

### Tests
- [x] Parity test: load each demo project from its dll and from its source folder, then compare `RelativePath` and `ContentHash` of all migrations, code items, tests, BeforeDeploy/AfterDeploy items, and the settings. Equal lists mean existing databases will not notice the change. (`EmbeddedScriptsParityTests`)
- [x] Unit tests for `EmbeddedFileSystem`: patterns, exclude patterns, subfolders, directories, a file with a BOM, `\` in resource names.
- [x] `SetProject` throws when the assembly has no scripts; `[SqlFact]` discovery works without `ProjectDir`.
- [x] Demo deployment tests and `[SqlFact]` tests pass with scripts read from the dll.

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
- [x] `SetProject(assembly)` reads embedded scripts, `SetScriptsFolder(path)` reads a folder, and `useVsProjectPath` is removed
- [x] The parity test passes for all demos
- [x] Demos and docs use the new `.csproj` block; no `ClearOutput` is left in code, demos or docs
- [x] "Go to source" still opens the `.sql` file (check in Visual Studio)

## Notes
2026-11-01, decided with the maintainer while reviewing 0096-10:
- Copying the scripts to `bin/` is not a feature: nobody looks at them there. Scripts belong in the dll by default; a folder is an explicit option (`SetScriptsFolder`), mainly for a future CLI.
- Keep `ProjectDir` for "Go to source", in every configuration.

Experiments on a copy of `DemoMSSQLEmpty`, .NET SDK 10.0.402:
- In a normal incremental build, the `IncrementalClean` target deletes a removed script from `bin/` (it tracks written files in `obj/*.FileListAbsolute.txt`), both in the SQL project and in a project that references it.
- With `obj/` deleted and `bin/` kept, the removed script stays in `bin/` with the demos' `ClearOutput`, and is deleted with `$(TargetDir)$(AssemblyName).Scripts`. In a referencing project, it stays with both.
- A dll built without `ProjectDir` already contains the absolute path of its `.pdb`, in Release too and with `ContinuousIntegrationBuild=true` in a folder without git. So `ProjectDir` is not what puts a build path into the dll.

2026-11-01, implementation:
- **`WithCulture`, verified** (copy of the new block, .NET SDK 10.0.402): without `WithCulture="false"`, `Code/dbo/name.fr.sql` and `Code/dbo/proc.de-DE.sql` went to `fr/` and `de-DE/` satellite assemblies and were missing from the main dll; with it, both stay. Resource names built on Windows look like `DbLive/Code\dbo\name.fr.sql`. `DbLive.Tests` embeds `Scripts/Code/v_fr.fr.sql` with the documented block, so a test fails if this ever changes.
- **`None Remove` is not needed**: without it, every script is both a `None` and an `EmbeddedResource` item (checked with `dotnet msbuild -getItem`), but Visual Studio lists each file once (checked by the maintainer in Solution Explorer). Not in the block.
- **Virtual root** is `<AssemblyName>.dll`, for example `DemoMSSQL.dll\Migrations\001.migration.sql` in `FileData.FilePath` and in `InvalidMigrationVersionException`. Relative, so it reads as "inside the dll"; `EmbeddedFileSystem` computes relative paths itself and never resolves the root against the current directory.
- **Pattern matching** uses `FileSystemName.MatchesWin32Expression(TranslateWin32Expression(pattern))`, not `MatchesSimpleExpression`: the legacy `Directory.EnumerateFiles(path, pattern)` overloads use `MatchType.Win32`. Same results for every pattern DbLive uses; differs for `*.*` and patterns with `?` at the end.
- **Case**: paths and patterns are matched ignoring case on every OS, like the physical file system on Windows. On Linux the physical file system was case-sensitive.
- **Order**: files and directories come in the order of `Directory.EnumerateFiles` on Windows (breadth-first, each folder sorted by name ignoring case, pattern by pattern), so code groups deploy in the same order as before on Windows. On Linux the order of the physical file system is arbitrary, so the parity test compares the items of each code group as a set (CI runs on Ubuntu).
- **Decoding**: `StreamReader` strips the UTF-8 preamble even without BOM detection; detection matters for UTF-16 files. Three demos contain UTF-8 BOM files. Mutation check: decoding as Latin-1 makes the parity test fail for those three demos.
- `SetScriptsFolder` resolves a relative path against the current directory and throws `DirectoryNotFoundException` for a missing folder, for the same reason `SetProject` throws `ScriptsNotEmbeddedException`.
- API: `IProjectPath` got `IFileSystem FileSystem`; `VisualStudioProjectPath` and `IDbLiveProject.GetVisualStudioProjectPath()` are nullable (null without `ProjectDir`); `ProjectPath(path, fileSystem, visualStudioProjectPath)` replaces both old constructors. `DbLiveProject` and `SettingsAccessor` no longer take `IFileSystem`; the `IFileSystem` singleton serves only `InternalDbLiveProject`. `TempProject.ProjectPath` is gone: integration tests call `SetScriptsFolder(project.Path)`.
- Tests: `DbLive.Tests` references `DbLive.xunit` and the four demo SQL projects (parity test), and is itself a SQL project without `ProjectDir` (`Scripts/`) for the `SetProject` and `[SqlFact]` discovery tests. All 380 tests pass locally (`dotnet test DbLive.sln --filter "Category!=LocalOnly"`, Docker): DbLive.Tests 311, DbLive.MSSQL.Tests 22, DbLive.PostgreSQL.Tests 11, DemoMSSQL.Tests 17, Demo.PostgreSQL.Chinook.Tests 10, AdventureWorks.Tests 9.
- "Go to source" in Visual Studio opens the `.sql` file with scripts read from the dll (checked by the maintainer).
- Follow-up: `IFileSystem` inside `IProjectPath` keeps `DbLiveProject` unchanged but makes `EmbeddedFileSystem` emulate a disk and leaves a second `IFileSystem` in DI. Replaced by a source of relative paths in 0096-31-01, which also embeds DbLive's own scripts (see "Out of scope").

## Result
- The SQL project embeds its scripts: `EmbeddedResource` with `LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)"` and `WithCulture="false"`, plus the `ProjectDir` assembly metadata. The copy to `<AssemblyName>.Scripts/`, `ClearOutput` and the `None`/`TargetPath` items are gone from the demos and the docs.
- `SetProject(assembly)` reads the embedded scripts and throws `ScriptsNotEmbeddedException` when the assembly has none; `SetScriptsFolder(path)` reads a folder (a relative path is resolved against the current directory, a missing folder throws `DirectoryNotFoundException`). `useVsProjectPath` is removed; the demo fixture builders call `SetProject(assembly)`.
- The first implementation (`EmbeddedFileSystem` behind `IProjectPath.FileSystem`) was replaced in 0096-31-01 by `IScriptSource` (`AssemblyScriptSource`, `FolderScriptSource`). 0096-31-01 also embedded DbLive's own scripts, the first item of "Out of scope".
- Found during the work: 0096-31-02 (`relative_path` is stored in lower case, so a database deploys the same from Windows and Linux) and 0096-31-02-01 (script paths that differ only by case throw `ScriptPathsDifferOnlyByCaseException`).
- "Go to source": `SqlFactDiscoverer` reads `ProjectDir` from the fixture builder's assembly; without it `SourceInformation` stays empty. Opens the `.sql` file in Visual Studio (checked by the maintainer).
- Tests: `EmbeddedScriptsParityTests` (all four demos, dll vs folder), `SqlFactDiscovererTests`, `DbLiveBuilderExtensionsTests`; `DbLive.Tests` is itself a SQL project without `ProjectDir`. All 407 tests pass after the last subtask, including the Docker integration tests and the demo tests.
- Docs: `README.md`, `docs/gettingstarted.md`, `docs/project/index.md`, `docs/project/structure.md`, `docs/deployment/index.md`, `docs/unit-testing/index.md`, `docs/unit-testing/stored-proc-and-unit-test.md`, `docs/llms.txt`, `docs/llms-full.txt`.
- Still out of scope, no task yet: keeping `ProjectDir` out of the production dll, and a standalone CLI deployer (it will use `SetScriptsFolder`).
- Commits: ec1bdc1 (this task), a1cd27d (0096-31-01), a49ba91 (0096-31-02), 39741f1 (0096-31-02-01).
