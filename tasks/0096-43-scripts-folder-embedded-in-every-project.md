---
type: bug
priority: medium
created: 2026-11-05
---
# Every project with DbLive in its graph embeds its `Scripts` folder

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
- [ ] `ProjectDir`: add it only when it is useful (not with `ContinuousIntegrationBuild`, or only in Debug)? Then "go to source" from a CI build does not work, which does not matter.

## Docs to update
- `docs/llms-full.txt` §0 rule 10, §3.1
- `docs/project/index.md` (embedding, `DbLiveEmbedScripts`)

## Done when
- [ ] A project that only has DbLive in its graph through a `ProjectReference` does not embed its `Scripts` folder, or the release note warns about it
- [ ] A test of the chosen behavior (the build tests of 0096-30-01)
- [ ] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96).
