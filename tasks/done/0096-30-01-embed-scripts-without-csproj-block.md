---
type: feature
priority: low
created: 2026-11-03
closed: 2026-11-03
---
# The packages embed the scripts without a `.csproj` block

## Release note
**The scripts folder can have another name.** `<DbLiveScriptsFolder>Db</DbLiveScriptsFolder>` in the `.csproj` of the SQL project embeds the `Db` folder instead of `Scripts`; the paths stored in the database stay relative to it, so renaming the folder redeploys nothing. Only a project that sets `DbLiveScriptsFolder` embeds anything, so other projects with DbLive in their graph and a `Scripts` folder of their own are not affected. A `.csproj` that sets it and embeds the DbLive scripts itself as well fails the build with "Remove the DbLive block from <project>.csproj".

## Goal
Every SQL project copies a block into its `.csproj` (`README.md` "Quick start" step 3, `docs/llms-full.txt` §3.1): an `EmbeddedResource` for `Scripts\**` and the `ProjectDir` assembly metadata. The attributes of the `EmbeddedResource` are easy to get wrong. Without `LogicalName` the folder structure is lost. Without `WithCulture="false"`, a `name.fr.sql` script silently moves to a satellite assembly and is not deployed. An MSBuild file in the package can add the same items, so the step disappears from the quick start, from every existing project and from the template (0096-30).

## Design
The file is in `buildTransitive/` of the `DbLive` package, so it reaches every project that has DbLive in its graph: through a provider package, through `DbLive.xunit` or through a `ProjectReference`. A layout B SQL project with only scripts references `DbLive` directly. When the project has the `$(DbLiveScriptsFolder)` folder, the package adds what the block adds today. Without the folder it adds nothing, not even `ProjectDir`:

```xml
<ItemGroup Condition="'$(DbLiveEmbedScripts)' == 'true' And Exists('$(MSBuildProjectDirectory)/$(DbLiveScriptsFolder)')">
  <EmbeddedResource Include="$(DbLiveScriptsFolder)\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />

  <AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
    <_Parameter1>ProjectDir</_Parameter1>
    <_Parameter2>$(ProjectDir)</_Parameter2>
  </AssemblyAttribute>
</ItemGroup>
```

Properties, set in the `.csproj`:
- `DbLiveScriptsFolder`, default `Scripts`: the folder to embed, relative to the project. Resource names stay relative to that folder (`DbLive/Migrations/001.migration.sql`), so the relative paths stored in the database do not depend on its name.
- `DbLiveEmbedScripts`, default `true`: `false` turns embedding off, for a project that gets the file but whose `Scripts` folder holds other files.

**Breaking.** A project that keeps the block gets the items twice, and the build fails with `NETSDK1022: Duplicate 'EmbeddedResource' items were included` (verified). That message advises `EnableDefaultEmbeddedResourceItems=false`, which does not fix it. A target in the file fails first with a message that says to remove the DbLive block from the `.csproj`.

- [x] Which package carries the file? Decided 2026-11-03: (a) with the folder check. Verified (Notes): NuGet imports `build/` only into a project that references the package directly. It imports `buildTransitive/` into every project that has the package anywhere in its graph, through package dependencies and through `ProjectReference`. Who gets the file in the layouts of `docs/llms-full.txt` §3.2:

  | Project | References | (a) `buildTransitive/` in `DbLive` | (b) `build/` in the providers | (c) `build/` in `DbLive` |
  |---|---|---|---|---|
  | Layout A `MyDb`: scripts and deployer | `DbLive.MSSQL`; `DbLive` too in the README | yes | yes | only with a direct `DbLive` reference |
  | Layout B `MyDb`: scripts only | no packages | no | no | no |
  | Layout B `MyDb.Deploy` | `MyDb`, `DbLive.MSSQL` | gets it, adds nothing without `Scripts` | gets it, adds nothing without `Scripts` | no |
  | `MyDb.Tests` | `MyDb`, the provider, `DbLive.xunit` | gets it, adds nothing without `Scripts` | gets it, adds nothing without `Scripts` | no |

  - In every option, the layout B SQL project needs a new package reference. With (b) it is the provider, which brings SqlClient and SMO into a library of scripts. With (a) and (c) it is `DbLive`.
  - (c) has a trap in layout A. The direct `DbLive` reference looks redundant next to the provider, which brings `DbLive` anyway, and `docs/llms-full.txt` §2 installs only the provider. Without that reference the build passes, and the first deployment fails with `ScriptsNotEmbeddedException`.
  - (a) with the folder check works however DbLive comes into the project (verified), and a project without the folder gets nothing. What is left: a project that has DbLive in its graph and an unrelated `Scripts` folder embeds that folder. `DbLiveEmbedScripts=false` turns this off.

## Where
- New `src/DbLive/buildTransitive/DbLive.props`, packed with `Pack="true" PackagePath="buildTransitive/"`. NuGet imports only files named after the package ID.
- `src/DbLive/Project/Exceptions/ScriptsNotEmbeddedException.cs:7-11`: the message shows the block. It should name the package reference and `DbLiveScriptsFolder`.
- Demos reference the projects, not the packages, so the package files do not reach them. `DemoMSSQL`, `DemoMSSQLEmpty`, `Demo.PostgreSQL.Chinook` and `AdventureWorks.Database` replace their blocks with an import of the file, for example from a `src/Demo/Directory.Build.props`. Such a file hides `src/Directory.Build.props` unless it imports it.
- `src/DbLive.Tests/DbLive.Tests.csproj:50-53` keeps its block. That project is a SQL project without `ProjectDir` on purpose (`SqlFactDiscovererTests`).
- `.github/workflows/cicd.yml:73-83`: the packages are packed here. The CI check through a real `PackageReference` below needs them in a local feed. The template test of 0096-30 uses the same feed.

## Plan
- [x] Answer the open question.
- [x] MSBuild file with `DbLiveScriptsFolder`, `DbLiveEmbedScripts` and the folder check, and a clear build error for a project that still has the block.
- [x] `ScriptsNotEmbeddedException` message.
- [x] Demos use the file instead of their blocks.
- [x] CI: a project with `Scripts/`, a `PackageReference` to the packed packages and no DbLive block in its `.csproj` has the scripts as `DbLive/...` resources (including a `name.fr.sql`) and the `ProjectDir` metadata. The same with `DbLiveScriptsFolder` set to another folder. A project without the folder gets neither.
- [x] "Go to source" of `[SqlFact]` with `DbLiveScriptsFolder` (found while implementing, Notes).
- [x] Docs.

## Docs to update
- `README.md` ("Quick start", steps 1 and 3)
- `docs/gettingstarted.md` (steps 2 and 4)
- `docs/project/index.md` ("The `.csproj` block", now "Embedding the scripts"; the layout table)
- `docs/project/structure.md` (first paragraph), `docs/unit-testing/index.md` (the link to the block)
- `src/DbLive/README.md` (the package page on nuget.org)
- `docs/llms-full.txt` §0 rule 10, §2, §3.1, §3.2, §3.4, §4, §13.2, §17 (`ScriptsNotEmbeddedException`, the build error, "Go to source"), §18.1

## Done when
- [x] A SQL project that references the package needs no change in its `.csproj`, tested in CI
- [x] Demos and docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.

2026-11-03: decisions of the maintainer:
- The upgrade is breaking: the block is removed from the `.csproj`. Rejected: detecting the block and silently adding nothing.
- `DbLiveScriptsFolder` is added.
- The file is in `buildTransitive/` of `DbLive` and adds nothing to a project without the scripts folder. Option (a) under Design. Rejected: (b) `build/` of the provider packages, which brings the provider into a library of scripts; (c) `build/` of `DbLive`, which needs a direct `DbLive` reference that looks redundant and, when missing, fails only at the first deployment.

2026-11-03, verified with test packages from a local feed (.NET SDK 10.0.402): `ExpCore` with `build/`, `ExpCoreT` with `buildTransitive/`, and `ExpProvider` that references both by `ProjectReference`, like `DbLive.MSSQL` references `DbLive`.
- `dotnet pack` writes a `ProjectReference` as a dependency with `exclude="Build,Analyzers"`.
- `build/` of `ExpCore` was imported only into a project with a direct `PackageReference` to `ExpCore`. It was not imported into a project that references only `ExpProvider`, nor into a project that references such a project by `ProjectReference`.
- `buildTransitive/` of `ExpCoreT` was imported into all three.

2026-11-03, verified the same way with the props of the Design in `buildTransitive/` of a core package, and a project that references only the package depending on it:
- `Scripts/Migrations/001.migration.sql` and `Scripts/Code/proc.fr.sql` became `DbLive/Migrations\001.migration.sql` and `DbLive/Code\proc.fr.sql`. There was no `fr/` satellite folder in `bin/`. The `ProjectDir` attribute was added.
- With `<DbLiveScriptsFolder>Db</DbLiveScriptsFolder>` in the `.csproj`, `Db/a.sql` became `DbLive/a.sql`.
- A project that references such a project by `ProjectReference` and has no `Scripts` folder got no resources and no `ProjectDir` attribute.
- With the old block left in the `.csproj`, the build failed with `NETSDK1022`.

2026-11-03, implementation:
- "Go to source" of `[SqlFact]` opened `<ProjectDir>/Scripts/<path>`, a missing file with `DbLiveScriptsFolder`. The file adds a second assembly metadata, `DbLiveScriptsFolder`, and `SqlFactDiscoverer.GetSourceScriptsPath` combines both; without it `Scripts`, for a `.csproj` that embeds the scripts itself with only `ProjectDir`. `\` and `/` in the folder work on every OS.
- The items are in the `.props`, imported before the `.csproj`, so a `.csproj` can `Remove` a single file from `EmbeddedResource`. The properties set in the `.csproj` still apply: MSBuild evaluates items after all properties.
- An empty `DbLiveScriptsFolder` embeds nothing. Without that check the item would be `\**\*`, the root of the drive. Verified.
- The check for the old block is a target with `BeforeTargets="CheckForDuplicateItems;CoreCompile"`. It fails when an `EmbeddedResource` that the file did not add (its items have the metadata `DbLiveScript="true"`) is one of the same files, or has a `LogicalName` that starts with `DbLive/`. Verified with a project that imports the file: the documented block, a block for another folder, and `<EmbeddedResource Include="Scripts\**\*" />` without `LogicalName` fail with the DbLive message, not `NETSDK1022`. A hand-written block with `DbLiveEmbedScripts=false` and an unrelated resource build.
- A `ProjectDir` `AssemblyAttribute` left alone in the `.csproj` is not an error: `AssemblyMetadataAttribute` allows several, both have the same value, and a check would also stop a project that adds the metadata for its own reason.
- `src/DbLive.Tests` keeps its block. It references the DbLive projects, not the packages, so it does not get the file.
- CI packs the four packages into `$RUNNER_TEMP/packages`, the local feed, and pushes them from there (`dotnet nuget push` expands the quoted wildcard and pushes the `.snupkg` next to each `.nupkg`). `.github/scripts/package-tests.cs` creates five projects in a temporary folder with their own NuGet cache and builds them against that feed. Verified with packages packed from this branch, on Windows and in the `mcr.microsoft.com/dotnet/sdk:10.0` container (Linux): all checks pass.
- The block was never in a released package: 0096-31, which introduced it, is closed after the latest release (1.5.0) and is not on `main` yet. The release of this branch gets both notes: 0096-31 says to replace the copy to the output folder with the block, this one says to remove the block. Users of 1.5.0 never had the block; only the docs published from this branch showed it.

2026-11-03, maintainer: this task is not breaking, and the release note of 0096-31 is rewritten without the block. No release needed the block, so users of 1.5.0 upgrade by removing their copy block (the note of 0096-31) and never see the error of this task. This replaces the decision "The upgrade is breaking" above. The note of this task tells about the new properties.

## Result
- `src/DbLive/buildTransitive/DbLive.props`, packed into `buildTransitive/` of the `DbLive` package: the `EmbeddedResource` of the scripts folder, the `ProjectDir` and `DbLiveScriptsFolder` assembly metadata, the properties `DbLiveScriptsFolder` and `DbLiveEmbedScripts`, the folder check, and the target `_DbLiveCheckScriptsEmbeddedTwice` with the error "Remove the DbLive block from <project>.csproj".
- `SqlFactDiscoverer.GetSourceScriptsPath` reads `DbLiveScriptsFolder` next to `ProjectDir`. Tests: `SqlFactDiscovererTests.GetSourceScriptsPath_FromDbLiveScriptsFolderMetadata`, `GetSourceScriptsPath_WithoutProjectDir_Null`.
- `ScriptsNotEmbeddedException` names the package reference, `DbLiveScriptsFolder` and `DbLiveEmbedScripts` instead of showing the block; `DbLiveBuilderExtensionsTests.SetProject_AssemblyWithoutScripts_Throws` checks it.
- Demos: the four SQL projects have no block; `src/Demo/Directory.Build.props` imports `src/Directory.Build.props` and the new file. Their assemblies have the same `DbLive/` resources as before, plus the `DbLiveScriptsFolder` metadata.
- CI (`.github/workflows/cicd.yml`): the packages are packed into `$RUNNER_TEMP/packages`, step "Test packages" runs `.github/scripts/package-tests.cs` against them, and "Publish packages" pushes from that folder. The checks: through `DbLive.PostgreSQL`, `Scripts/` with `Code/proc.fr.sql` and a subfolder is embedded as `DbLive/...` with both metadata and no `fr/` folder; with `DbLiveScriptsFolder` `Db` only `Db/` is embedded; a deployer with a `ProjectReference` to the SQL project and no folder gets nothing; `DbLiveEmbedScripts=false` gets nothing; the old block fails the build with the DbLive message.
- `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 501 passed, 0 failed (DbLive.Tests 395, DbLive.MSSQL.Tests 49, DbLive.PostgreSQL.Tests 36, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4), with SQL Server and PostgreSQL in Docker. `mkdocs build --strict` passes.
- Docs: listed under "Docs to update".
