---
type: feature
priority: low
created: 2026-11-03
---
# The packages embed the scripts without a `.csproj` block

## Goal
Every SQL project copies a block into its `.csproj` (`README.md` "Quick start" step 3, `docs/llms-full.txt` §3.1): an `EmbeddedResource` for `Scripts\**` and the `ProjectDir` assembly metadata. The attributes of the `EmbeddedResource` are easy to get wrong. Without `LogicalName` the folder structure is lost. Without `WithCulture="false"`, a `name.fr.sql` script silently moves to a satellite assembly and is not deployed. An MSBuild file in the package can add the same items, so the step disappears from the quick start, from every existing project and from the template (0096-30).

## Design
When the project has the `$(DbLiveScriptsFolder)` folder, the package adds what the block adds today. Without the folder it adds nothing, not even `ProjectDir`:

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

- [ ] Which package carries the file? Verified (Notes): NuGet imports `build/` only into a project that references the package directly. It imports `buildTransitive/` into every project that has the package anywhere in its graph, through package dependencies and through `ProjectReference`. Who gets the file in the layouts of `docs/llms-full.txt` §3.2:

  | Project | References | (a) `buildTransitive/` in `DbLive` | (b) `build/` in the providers | (c) `build/` in `DbLive` |
  |---|---|---|---|---|
  | Layout A `MyDb`: scripts and deployer | `DbLive.MSSQL`; `DbLive` too in the README | yes | yes | only with a direct `DbLive` reference |
  | Layout B `MyDb`: scripts only | no packages | no | no | no |
  | Layout B `MyDb.Deploy` | `MyDb`, `DbLive.MSSQL` | gets it, adds nothing without `Scripts` | gets it, adds nothing without `Scripts` | no |
  | `MyDb.Tests` | `MyDb`, the provider, `DbLive.xunit` | gets it, adds nothing without `Scripts` | gets it, adds nothing without `Scripts` | no |

  - In every option, the layout B SQL project needs a new package reference. With (b) it is the provider, which brings SqlClient and SMO into a library of scripts. With (a) and (c) it is `DbLive`.
  - (c) has a trap in layout A. The direct `DbLive` reference looks redundant next to the provider, which brings `DbLive` anyway, and `docs/llms-full.txt` §2 installs only the provider. Without that reference the build passes, and the first deployment fails with `ScriptsNotEmbeddedException`.
  - Proposal: (a) with the folder check. It works however DbLive comes into the project (verified), and a project without the folder gets nothing. What is left: a project that has DbLive in its graph and an unrelated `Scripts` folder embeds that folder. `DbLiveEmbedScripts=false` turns this off.

## Where
- New MSBuild file in the package chosen above (`buildTransitive/` or `build/`).
- `src/DbLive/Project/Exceptions/ScriptsNotEmbeddedException.cs:7-11`: the message shows the block. It should name the package reference and `DbLiveScriptsFolder`.
- Demos reference the projects, not the packages, so the package files do not reach them. `DemoMSSQL`, `DemoMSSQLEmpty`, `Demo.PostgreSQL.Chinook` and `AdventureWorks.Database` replace their blocks with an import of the file, for example from a `src/Demo/Directory.Build.props`. Such a file hides `src/Directory.Build.props` unless it imports it.
- `src/DbLive.Tests/DbLive.Tests.csproj:50-53` keeps its block. That project is a SQL project without `ProjectDir` on purpose (`SqlFactDiscovererTests`).
- `.github/workflows/cicd.yml:73-83`: the packages are packed here. The CI check through a real `PackageReference` below needs them in a local feed. The template test of 0096-30 uses the same feed.

## Plan
- [ ] Answer the open question.
- [ ] MSBuild file with `DbLiveScriptsFolder`, `DbLiveEmbedScripts` and the folder check, and a clear build error for a project that still has the block.
- [ ] `ScriptsNotEmbeddedException` message.
- [ ] Demos use the file instead of their blocks.
- [ ] CI: a project with `Scripts/`, a `PackageReference` to the packed packages and no DbLive block in its `.csproj` has the scripts as `DbLive/...` resources (including a `name.fr.sql`) and the `ProjectDir` metadata. The same with `DbLiveScriptsFolder` set to another folder. A project without the folder gets neither.
- [ ] Docs.

## Docs to update
- `README.md` ("Quick start", steps 1 and 3)
- `docs/gettingstarted.md` (steps 2 and 4)
- `docs/project/index.md` ("The `.csproj` block", the layout table)
- `docs/llms-full.txt` §0 rule 10, §2, §3.1, §3.2, §3.4, §17 (`ScriptsNotEmbeddedException`), §18.1

## Done when
- [ ] A SQL project that references the package needs no change in its `.csproj`, tested in CI
- [ ] Demos and docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.

2026-11-03: decisions of the maintainer:
- The upgrade is breaking: the block is removed from the `.csproj`. Rejected: detecting the block and silently adding nothing.
- `DbLiveScriptsFolder` is added.

2026-11-03, verified with test packages from a local feed (.NET SDK 10.0.402): `ExpCore` with `build/`, `ExpCoreT` with `buildTransitive/`, and `ExpProvider` that references both by `ProjectReference`, like `DbLive.MSSQL` references `DbLive`.
- `dotnet pack` writes a `ProjectReference` as a dependency with `exclude="Build,Analyzers"`.
- `build/` of `ExpCore` was imported only into a project with a direct `PackageReference` to `ExpCore`. It was not imported into a project that references only `ExpProvider`, nor into a project that references such a project by `ProjectReference`.
- `buildTransitive/` of `ExpCoreT` was imported into all three.

2026-11-03, verified the same way with the props of the Design in `buildTransitive/` of a core package, and a project that references only the package depending on it:
- `Scripts/Migrations/001.migration.sql` and `Scripts/Code/proc.fr.sql` became `DbLive/Migrations\001.migration.sql` and `DbLive/Code\proc.fr.sql`. There was no `fr/` satellite folder in `bin/`. The `ProjectDir` attribute was added.
- With `<DbLiveScriptsFolder>Db</DbLiveScriptsFolder>` in the `.csproj`, `Db/a.sql` became `DbLive/a.sql`.
- A project that references such a project by `ProjectReference` and has no `Scripts` folder got no resources and no `ProjectDir` attribute.
- With the old block left in the `.csproj`, the build failed with `NETSDK1022`.
