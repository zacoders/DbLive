---
type: feature
priority: low
created: 2026-11-03
---
# The packages embed the scripts without a `.csproj` block

## Goal
Every SQL project copies a block into its `.csproj` (`README.md` "Quick start" step 3, `docs/llms-full.txt` §3.1): an `EmbeddedResource` for `Scripts\**` and the `ProjectDir` assembly metadata. The attributes of the `EmbeddedResource` are easy to get wrong. Without `LogicalName` the folder structure is lost. Without `WithCulture="false"`, a `name.fr.sql` script silently moves to a satellite assembly and is not deployed. An MSBuild file in the package can add the same items, so the step disappears from the quick start, from every existing project and from the template (0096-30).

## Design
When the project has a `Scripts` folder, the package adds what the block adds today:

```xml
<EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />

<AssemblyAttribute Include="System.Reflection.AssemblyMetadata">
  <_Parameter1>ProjectDir</_Parameter1>
  <_Parameter2>$(ProjectDir)</_Parameter2>
</AssemblyAttribute>
```

`<DbLiveEmbedScripts>false</DbLiveEmbedScripts>` turns it off. Use it, for example, in an ASP.NET app with a `Scripts` folder of JavaScript that references DbLive to host the deployer.

- [ ] Which package carries the file? NuGet does not import `build/` of a package that comes in only as a dependency (to verify), and `docs/llms-full.txt` §2 says `DbLive` comes with the provider package. So `DbLive/build/` would not reach a project that references only `DbLive.MSSQL`. Options: (a) `buildTransitive/` in `DbLive`; then every project that gets DbLive through any chain gets it too, including a project that references the SQL project. (b) `build/` in each provider package, from one shared source file; the SQL project references the provider package directly. Proposal: (b).
- [ ] A project that already has the block gets the items twice; duplicate resource names fail the build (to verify). Should the file add nothing when the project already has an `EmbeddedResource` whose `LogicalName` starts with `DbLive/`, or should the upgrade be breaking ("remove the block from the `.csproj`")?
- [ ] A `DbLiveScriptsFolder` property for a folder not named `Scripts`, or is the block in the `.csproj` enough for that case?

## Where
- New MSBuild file, packed into the package chosen above (`Pack="true" PackagePath="build/"`).
- Demos reference the projects, not the packages, so the package files do not reach them. `DemoMSSQL`, `DemoMSSQLEmpty`, `Demo.PostgreSQL.Chinook` and `AdventureWorks.Database` replace their blocks with an import of the file, for example from a `src/Demo/Directory.Build.props`. Such a file hides `src/Directory.Build.props` unless it imports it.
- `src/DbLive.Tests/DbLive.Tests.csproj:50-53` keeps its block. That project is a SQL project without `ProjectDir` on purpose (`SqlFactDiscovererTests`).
- `.github/workflows/cicd.yml:73-83`: the packages are packed here. The CI check through a real `PackageReference` below needs them in a local feed. The template test of 0096-30 uses the same feed.

## Plan
- [ ] Answer the open questions.
- [ ] MSBuild file in the package, with `DbLiveEmbedScripts`.
- [ ] Demos use it instead of their blocks.
- [ ] CI: a project with `Scripts/` and a `PackageReference` to the packed packages, with no DbLive block in its `.csproj`, has the scripts as `DbLive/...` resources (including a `name.fr.sql`) and the `ProjectDir` metadata.
- [ ] Docs.

## Docs to update
- `README.md` ("Quick start", step 3)
- `docs/gettingstarted.md` (step 4)
- `docs/project/index.md` ("The `.csproj` block")
- `docs/llms-full.txt` §3.1, §18.1

## Done when
- [ ] A SQL project that references the package needs no change in its `.csproj`, tested in CI
- [ ] Demos and docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.
