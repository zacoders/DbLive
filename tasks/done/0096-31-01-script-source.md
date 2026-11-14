---
type: chore
priority: medium
created: 2026-11-01
closed: 2026-11-01
breaking: true
---
# Read scripts through a source of relative paths; embed DbLive's own scripts

## Release note
**Scripts are read from the SQL project assembly or a folder, the same way on every OS.** `IProjectPath`, `ProjectPath`, `IFileSystem` and `FileSystem` are removed. If you registered `IProjectPath` yourself to read the scripts from a folder, call `SetScriptsFolder(path)` instead; a source other than the assembly (`SetProject`) or a folder (`SetScriptsFolder`) is not supported. Files are found and ordered the same way on every OS: file name patterns (`*.test.sql`, `TestFilePatterns`) ignore case, and the code files of a group, the tests and the BeforeDeploy/AfterDeploy scripts run in the order of their relative paths, ignoring case (before, the order depended on the OS). DbLive's own scripts (the `dblive` schema) are embedded in `DbLive.MSSQL.dll` and `DbLive.PostgreSQL.dll`: nothing is copied to your output folder any more, and a `DbLive.MSSQL/` or `DbLive.PostgreSQL/` folder left there by an older version can be deleted.

## Goal
0096-31 embedded the scripts in the SQL project assembly by adding `IFileSystem FileSystem` to `IProjectPath`, so that `DbLiveProject` stayed unchanged. That kept the change small, but left four problems:
- **`IProjectPath` is no longer a path.** It carries the root, the way to read files, and a path for the IDE.
- **Two `IFileSystem` instances, and a trap.** The DI singleton is the physical `FileSystem`, used only for DbLive's own scripts (`InternalDbLiveProject`); the project's file system sits inside `IProjectPath`. A new class that injects `IFileSystem` to read project files works with `SetScriptsFolder` and silently reads the disk with `SetProject`. A service inside a data object is a service locator.
- **The interface is shaped after the disk**: absolute paths, directories, `SearchOption`, `ReadFileDataAsync(path, rootPath)`. So `EmbeddedFileSystem` (212 lines) emulates a disk: a virtual root (`DemoMSSQL.dll\Migrations\...`), the Windows enumeration order, Win32 patterns, directories derived from file names. `DbLiveProject` needs much less: the list of files by relative path, and a way to read one.
- **"Go to source" lives in the core** (`IProjectPath.VisualStudioProjectPath`, `IDbLiveProject.GetVisualStudioProjectPath()`), although only `DbLive.xunit` uses it, and its fixture already knows the assembly.

DbLive's own scripts (the `dblive` schema) have the same problem as the project scripts had before 0096-31: `DbLive.MSSQL` and `DbLive.PostgreSQL` copy them into the user's `bin/` with `buildTransitive` targets, and the self-deployer reads them from `<AppContext.BaseDirectory>/DbLive.MSSQL/`. They move into the provider assemblies in this task, with the same source.

The maintainer is the only user so far, so the API may break. Existing databases must keep deploying with no changes: every project script keeps its `RelativePath` and content (guarded by `EmbeddedScriptsParityTests`). DbLive's own scripts are compared with the database only by version (`DbLiveSelfDeployer`, `dblive.version`), so they only need to keep their file names.

## Design
```csharp
namespace DbLive.Project;

/// <summary>
/// The files of a DbLive project.
/// </summary>
public interface IScriptSource
{
    /// <summary>
    /// Where the scripts come from, for logs and exception messages: "DemoMSSQL.dll" or the folder path.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// Relative paths of all files, with '/': "Migrations/001.migration.sql".
    /// </summary>
    IReadOnlyList<string> GetFiles();

    Task<string> ReadAsync(string relativePath);
}
```

| Implementation | Used for | Files | Read |
| --- | --- | --- | --- |
| `AssemblyScriptSource(assembly, prefix)` | `SetProject(assembly)` with prefix `DbLive/`; DbLive's own scripts with prefix `DbLive.Internal/` | resource names with the prefix, `\` replaced by `/` | `StreamReader`, UTF-8 with BOM detection |
| `FolderScriptSource(path)` | `SetScriptsFolder(path)` | `Directory.EnumerateFiles(path, "*", AllDirectories)`, relative, `\` replaced by `/` | `File.ReadAllTextAsync` |

Project scripts:
- `DbLiveProject` and `SettingsAccessor` depend on `IScriptSource` and work with relative paths only. A folder from the settings (`Code`, `db\Code`, `/Code/`) becomes the prefix `Code/`; a file is in a folder when its path starts with the prefix, ignoring case; patterns (`*.sql`, `TestFilePatterns`) match the file name with `FileSystemName.MatchesWin32Expression`, as now. The folder of a test is its path up to the last `/`.
- `GetFiles()` is a method, not a cached property: `FolderScriptSource` enumerates the folder on every call, because tests change files between deployments with the same builder (`TempProject.DeleteFiles` in `ItemTransactionSettingsTests.cs:57`).
- Order: code items within a group and BeforeDeploy/AfterDeploy items are sorted by relative path with `StringComparer.OrdinalIgnoreCase`, the same on every OS and in every culture.
- `FileData.FilePath` is removed. `FileData.FileName` comes from `RelativePath`; messages show `RelativePath`.
- `CodeGroup.Path` is the relative folder (`Code`, `Code/sub1`).
- `RelativePath` takes the real names of files and folders. Today, on Windows, the folder part takes its casing from the settings (`"CodeFolder": "code"` with the folder `Code` gives `code/x.sql`), because `Directory.EnumerateFiles` returns the directory as it was passed. A database deployed that way sees its code items as renamed once. 0096-31-02 makes the casing irrelevant.
- `IProjectPath`, `ProjectPath` and `EmbeddedFileSystem` are removed.

DbLive's own scripts:
- The providers embed `SQL\**\*.sql` with `LogicalName="DbLive.Internal/%(RecursiveDir)%(Filename)%(Extension)"` and `WithCulture="false"`. A prefix other than `DbLive/`, so that `SetProject(typeof(MsSqlDA).Assembly)` by mistake throws `ScriptsNotEmbeddedException` instead of deploying DbLive's schema scripts as the user's migrations.
- `InternalDbLiveProject` depends on its own type, for example `IInternalScriptSource : IScriptSource`, registered by `InitializeMSSQL()` / `InitializePostgreSQL()` with the provider assembly. A separate type keeps it apart from the project's `IScriptSource` in DI.
- Removed: `IInternalProjectPath`, `MsSqlProjectPath`, `PostgreSqlProjectPath`, `IFileSystem`, `FileSystem`, `DbLive.MSSQL.targets`, `DbLive.PostgreSQL.targets`, and in both provider `.csproj` the `None` items that copy `SQL\**` to the output and pack it into `scripts/` and `buildTransitive/`.
- A `DbLive.MSSQL/` or `DbLive.PostgreSQL/` folder left in `bin/` by an older package version is no longer read.

"Go to source":
- `SqlFactDiscoverer` reads `ProjectDir` from the fixture's assembly (`DbLiveTestFixture` has `fixtureBuilder.GetProjectAssembly()`) and builds `<ProjectDir>/Scripts/<RelativePath>`. `GetVisualStudioProjectPath()` is removed from `IDbLiveProject`; `GetSourceScriptsPath` moves from `DbLiveBuilderExtensions` to `DbLive.xunit`.

## Where
- `src/DbLive/Common/IProjectPath.cs`, `src/DbLive/Common/EmbeddedFileSystem.cs`, `src/DbLive/Common/IFileSystem.cs`, `src/DbLive/Common/FileSystem.cs`.
- `src/DbLive/Project/DbLiveProject.cs`: `GetCodeGroupsAsync` (21), `GetMigrationsAsync` (94), `GetTestsAsync` and `GetFolderTestsAsync` (171, 201), `GetFolderItemsAsync` (240, order at 267), `GetVisualStudioProjectPath` (271).
- `src/DbLive/Project/ProjectSettingsReader.cs` (`SettingsAccessor`), `src/DbLive/Project/IDbLiveProject.cs:26`, `src/DbLive/Project/CodeGroup.cs`.
- `src/DbLive/DbLiveBuilderExtensions.cs`: `SetProject`, `SetScriptsFolder`, `GetSourceScriptsPath`.
- `src/DbLive/Bootstrapper.cs:23` (the `IFileSystem` singleton).
- `src/DbLive/Common/FileData.cs:19-21`, `src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:107`, `src/DbLive/Deployers/Folder/FolderDeployer.cs:36`, `src/DbLive/Project/MigrationVersionValidator.cs` (gets the file path for `InvalidMigrationVersionException`).
- DbLive's own scripts: `src/DbLive/SelfDeployer/InternalDbLiveProject.cs`, `src/DbLive/Adapter/IInternalProjectPath.cs`, `src/DbLive.MSSQL/MsSqlProjectPath.cs`, `src/DbLive.MSSQL/Bootstrapper.cs`, `src/DbLive.MSSQL/DbLive.MSSQL.csproj`, `src/DbLive.MSSQL/DbLive.MSSQL.targets`, and the same four files in `src/DbLive.PostgreSQL/`. Check whether `NoWarn NU5129` in both provider `.csproj` is still needed without the targets.
- `src/DbLive.xunit/SqlTest/SqlFactDiscoverer.cs:71`, `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs`.
- Tests: `src/DbLive.Tests/Project/` (`CodeItemsTests`, `MigrationsTests`, `SqlTestingTests`, `GetFolderItemsTests`, `SettingsTests`, `ProjectFoldersTests`: about 100 calls to the `IFileSystem` and `IProjectPath` mocks; `ConstructorTests` is all commented out), `EmbeddedScriptsParityTests.cs`, `src/DbLive.Tests/Common/EmbeddedFileSystemTests.cs`, `src/DbLive.Tests/Builder/DbLiveBuilderTests.cs`, `src/DbLive.Tests/Builder/DbLiveBuilderExtensionsTests.cs`, `src/DbLive.Tests/SqlTest/SqlFactDiscovererTests.cs`, `src/DbLive.Tests/SelfDeployer/InternalDbLiveProjectTests.cs`, `src/DbLive.Tests.Common/MockSet.cs`; `FilePath = ` in about 30 `FileData` initializers across `src/DbLive.Tests`.

## Plan
- [x] `IScriptSource`, `AssemblyScriptSource`, `FolderScriptSource`. `SetProject` and `SetScriptsFolder` register them and keep their checks (`ScriptsNotEmbeddedException`, `DirectoryNotFoundException`).
- [x] `DbLiveProject` and `SettingsAccessor` on relative paths, with one helper for "the files of folder X, with or without subfolders, matching these patterns and not those", so the discovery methods do not each repeat the filtering.
- [x] Remove `FileData.FilePath`; `CodeGroup.Path` relative; the new order of code and folder items.
- [x] Remove `IProjectPath`, `ProjectPath` and `EmbeddedFileSystem`.
- [x] DbLive's own scripts embedded in the providers; `InternalDbLiveProject` reads them through `IInternalScriptSource`; remove `IInternalProjectPath`, the provider path classes, the targets, `IFileSystem` and `FileSystem`.
- [x] "Go to source" in `DbLive.xunit`; remove `GetVisualStudioProjectPath()` from `IDbLiveProject`.

### Tests
- [x] An in-memory `IScriptSource` in `DbLive.Tests.Common`. `MockSet` registers it instead of the `IFileSystem` and `IProjectPath` mocks, and the `DbLive.Tests/Project` tests add files to it instead of mocking each `IFileSystem` call. `InternalDbLiveProjectTests` use it too.
- [x] `AssemblyScriptSource` and `FolderScriptSource`: relative paths with `/`, `\` in resource names, the same content and hash for UTF-8 with and without a BOM and for UTF-16 (move these cases from `EmbeddedFileSystemTests`), a culture-named file stays in the assembly (`v_fr.fr.sql`).
- [x] `FolderScriptSource` sees files added and deleted after the builder was created.
- [x] The parity test also compares the order within code groups, now that it does not depend on the OS.
- [x] Both provider assemblies embed all their `SQL/*.sql` files with the `DbLive.Internal/` prefix, and the versions are 1..N without gaps. `SetProject` with a provider assembly throws `ScriptsNotEmbeddedException`.
- [x] `[SqlFact]` discovery with and without `ProjectDir`.
- [x] All tests pass, including the Docker integration tests and the demo tests (every integration test self-deploys DbLive's schema into a new database).
- [x] Packages: `dotnet pack` of `DbLive.MSSQL` and `DbLive.PostgreSQL` contains no `scripts/` folder and no `buildTransitive` targets, and a project that references the packed packages (`src/build-nuget-local.cmd`) deploys with nothing next to its dll.

## Docs to update
- `docs/llms-full.txt`: §2 (the provider package copies DbLive's own scripts to the output); §3.4 (the virtual root `<AssemblyName>.dll` in `FileData.FilePath`); §4 table and §7 (order of BeforeDeploy/AfterDeploy: by relative path, ordinal, ignoring case); §19 (DbLive's own scripts are still copied).
- `docs/project/index.md`: section "Provider files" and the sentence "DbLive's own scripts are still files" in "How DbLive finds the scripts".
- `docs/project/structure.md`: folder table (order of BeforeDeploy/AfterDeploy).
- `src/DbLive.MSSQL/README.md:19`, `src/DbLive.PostgreSQL/README.md:19`: "The package copies its internal SQL scripts ... keep that folder next to your deployer."

## Done when
- [x] `DbLiveProject` and `SettingsAccessor` read through `IScriptSource`; no `IProjectPath`, `EmbeddedFileSystem`, `IFileSystem` or `VisualStudioProjectPath` is left in `DbLive`
- [x] DbLive's own scripts are read from the provider assemblies; no targets copy them to `bin/`
- [x] The parity test passes for all demos
- [x] "Go to source" still opens the `.sql` file (check in Visual Studio)
- [x] Docs updated

## Notes
2026-11-01, decided with the maintainer while reviewing 0096-31. Options considered:
- A: keep `IFileSystem` in `IProjectPath` and only rename the interface. Fixes the name only.
- B: a separate `IProjectFileSystem : IFileSystem` registered in DI. Removes the DI trap, keeps the disk emulation.
- C: this task, with "Go to source" moved to `DbLive.xunit`.

2026-11-01, decisions on the open questions:
- `FileData.FilePath`: removed. Production code reads it only through `FileData.FileName` (one log line in `FolderDeployer`); `MigrationVersionValidator` gets the path from `MigrationItemInfo`, which becomes relative too.
- `CodeGroup.Path`: kept, relative. Only tests read it today.
- `InternalDbLiveProject`: moved in this task, so `IFileSystem` and `FileSystem` go away. At the maintainer's request, DbLive's own scripts are also embedded in the provider assemblies here, instead of in a separate task.
- Order of code items within a group: sorted by relative path, `OrdinalIgnoreCase`. Today it is breadth-first on Windows and arbitrary on Linux; code deploys in parallel with retries, so the order only changes which files start first.
- Order of BeforeDeploy/AfterDeploy items: sorted by relative path, `OrdinalIgnoreCase`. Today `OrderBy` on the full path uses the current culture, so the order can differ between machines. The demos have one file in each folder, so their order does not change.
- Casing of `RelativePath`: the real names of files and folders, not the casing from the settings. Storing relative paths in lower case in `dblive` was considered too; it is its own task, 0096-31-02, because it changes `dblive` data in existing databases.

2026-11-01, implementation:
- **Types.** `IScriptSource` is public in `DbLive.Project` (`DbLiveProject` and `SettingsAccessor` are public and take it); `AssemblyScriptSource`, `FolderScriptSource` and the `ReadFileDataAsync(relativePath)` extension are internal. `IInternalScriptSource` is public in `DbLive.SelfDeployer`, because `MockSet` in `DbLive.Tests.Common` has no access to internals; its implementation `InternalScriptSource(providerAssembly)` is internal and is registered by `InitializeMSSQL()` / `InitializePostgreSQL()` with `typeof(MsSqlDA).Assembly` / `typeof(PostgreSqlDA).Assembly`. `AssemblyScriptSource.ReadAsync` looks a path up exactly (`Ordinal`): paths always come from `GetFiles()`, and an ignore-case dictionary would throw on two resources that differ only by case (possible when built on Linux).
- **One helper, always recursive.** `GetFolderFiles(files, folder, patterns, excludePatterns)` in `DbLiveProject` filters by folder prefix and file name patterns and sorts (`OrdinalIgnoreCase`, then `Ordinal` for names that differ only by case). "Without subfolders" turned out not to be needed: `GetTestsAsync` no longer walks folder by folder, it takes all tests of the Tests and Code folders and looks the setup file up by the folder of each test, reading it once per folder. `GetFolderTestsAsync` is gone.
- **Order of tests.** Tests are sorted by relative path too (Tests folder first, then Code). Tests run in parallel, so this changes nothing at run time; the parity test now compares code groups and tests in their order.
- **No code, no groups.** `GetCodeGroupsAsync` returns no groups when the code folder has no code files. Before, it returned none only when the folder did not exist; a code folder with only tests gave empty groups and a warning per `CodeSubFoldersDeploymentOrder` entry.
- **Empty provider assembly.** `InternalDbLiveProject` throws `InternalException` ("DbLive's own scripts are not embedded in DbLive.MSSQL.dll") when the source has no files. Before, a missing folder threw `DirectoryNotFoundException`; with resources an empty list would silently deploy no `dblive` schema.
- **`Location`** is logged at the start of a deployment (`Starting project deploy from DemoMSSQL.dll.`, `DbLiveDeployer` takes `IScriptSource`) and is in the `FileNotFoundException` messages of the sources. `FolderDeployer` logs `RelativePath` instead of the file name.
- **Renames and removals.** `MigrationItemInfo.FilePath` → `RelativePath`; `MigrationFileNameParser.GetMigrationInfo(relativePath)`. `StringExtensions.CombineWith` and `CombineWithRelative` are removed (no callers left); `GetRelativePath` stays for `FolderScriptSource`. `ConstructorTests.cs` (all commented out, about the long-gone `ProjectPathAccessor`) is deleted. `EmbeddedFileSystemTests` became `ScriptSourceTests`.
- **`NoWarn NU5129`** is removed from both providers: `dotnet pack` gives no warnings without the targets (the build has `TreatWarningsAsErrors`).
- **Test wiring.** `DbLive.Tests` references both providers and gets a `SrcDir` assembly metadata (`src/`), so `InternalScriptsTests` compare the embedded scripts with `SQL/` of each provider. `DbLive.xunit` has `InternalsVisibleTo DbLive.Tests` for `SqlFactDiscoverer.GetSourceScriptsPath`, used by the parity test. `DbLiveTestFixture.GetProjectAssembly()` is internal.
- **Verified.** All 385 tests pass locally (`dotnet test DbLive.sln --filter "Category!=LocalOnly"`, Docker): DbLive.Tests 316, DbLive.MSSQL.Tests 22, DbLive.PostgreSQL.Tests 11, DemoMSSQL.Tests 17, Demo.PostgreSQL.Chinook.Tests 10, AdventureWorks.Tests 9. Packages: `dotnet pack -p:PackageVersion=0.0.1-local` of `DbLive`, `DbLive.MSSQL`, `DbLive.PostgreSQL` into a temp folder (not `build-nuget-local.cmd`, to keep `C:\nuget-local` and the NuGet cache untouched); the provider packages contain only `lib/*/<provider>.dll`, `README.md` and the icon. A console app referencing `DbLive.PostgreSQL 0.0.1-local` (own `RestorePackagesPath`) had only `DbLive.dll` and `DbLive.PostgreSQL.dll` in `bin/`, and deployed a migration and a code item into a PostgreSQL container: `dblive.version` = 5.
- **Docs.** Besides the listed sections, `docs/llms-full.txt` §3.4 (relative paths only, casing, `Location` in the log, "Go to source" independent of `SetProject`/`SetScriptsFolder`), §4 (matching ignores case; the order with an example), §11.1 (a custom `IScriptSource`), §13.2 (`ProjectDir` of the fixture builder's assembly). The `\` in the examples of §6 and §8.1 is left to 0096-31-02, which rewrites those lines.
- "Go to source" in Visual Studio opens the `.sql` file (checked by the maintainer). `SqlFactDiscovererTests` checks that `SourceInformation.FileName` is `<ProjectDir>/Scripts/<RelativePath>` and that the file exists.
- 2026-11-14, after closing: 0096-80 (same release) makes `IScriptSource` internal and removes `SetScriptSource` (decided by the maintainer). The release note above no longer names `IScriptSource` and no longer offers a source of your own.

## Result
- `IScriptSource` (`Location`, `GetFiles()`, `ReadAsync(relativePath)`) replaces `IProjectPath`, `ProjectPath`, `EmbeddedFileSystem`, `IFileSystem` and `FileSystem`. `SetProject(assembly)` registers `AssemblyScriptSource(assembly, "DbLive/")`, `SetScriptsFolder(path)` registers `FolderScriptSource(path)`, which enumerates the folder on every call.
- `DbLiveProject` and `SettingsAccessor` work with relative paths only: folders from the settings become prefixes, patterns match file names ignoring case, every discovery method filters through `GetFolderFiles` and gets the files sorted by relative path (`OrdinalIgnoreCase`). Code items within a group, tests and BeforeDeploy/AfterDeploy items come in that order on every OS. The setup file of a test is looked up by the test's folder. `FileData.FilePath` is removed, `MigrationItemInfo.FilePath` is `RelativePath`, `CodeGroup.Path` is relative.
- DbLive's own scripts are embedded in `DbLive.MSSQL.dll` and `DbLive.PostgreSQL.dll` as `DbLive.Internal/...`; `InternalDbLiveProject` reads them through `IInternalScriptSource`, registered by the providers. The `.targets`, the copy to `bin/`, the `scripts/` folder in the packages and `NoWarn NU5129` are gone, as are `IInternalProjectPath`, `MsSqlProjectPath` and `PostgreSqlProjectPath`.
- "Go to source": `SqlFactDiscoverer.GetSourceScriptsPath` reads `ProjectDir` from the fixture builder's assembly; `IDbLiveProject.GetVisualStudioProjectPath()` is removed.
- Tests: `InMemoryScriptSource` in `DbLive.Tests.Common` replaces the `IFileSystem`/`IProjectPath` mocks; new `ScriptSourceTests`, `InternalScriptsTests`, `[SqlFact]` discovery with `ProjectDir`, `FolderScriptSource` sees added and deleted files; the parity test compares the order of code groups and tests. 385 tests pass (see Notes).
- Docs: `docs/llms-full.txt` §2, §3.4, §4, §7, §11.1, §13.2, §19; `docs/project/index.md`, `docs/project/structure.md`; provider READMEs.
