---
type: bug
priority: medium
created: 2026-10-26
---
# Demo `ClearOutput` deletes the wrong folder

## Problem
The demos delete `$(TargetDir)$(AssemblyName)`, but scripts are copied to `$(AssemblyName).Scripts`. Deleted and renamed scripts stay in `bin/` and keep being deployed. People and agents copy the demos as a template.

## Where
- `src/Demo/DemoMSSQL/DemoMSSQL.csproj:11`
- `src/Demo/DemoMSSQLEmpty/DemoMSSQLEmpty.csproj:11`
- `src/Demo/Demo.PostgreSQL.Chinook/Demo.PostgreSQL.Chinook.csproj:15`
- `src/Demo/AdventureWorks/AdventureWorks.Database/AdventureWorks.Database.csproj:10`

## Fix
Replace with `$(TargetDir)$(AssemblyName).Scripts`. Idea for later: ship this block as MSBuild targets in the DbLive package, so users don't copy it by hand.

## Docs to update
The snippets in `README.md`, `docs/gettingstarted.md`, `docs/project/index.md` and `docs/llms-full.txt` are already fixed. After fixing the demos:
- `docs/llms-full.txt` §3.1: remove the paragraph "Note: older DbLive docs and the repository demos use...".
- If the block moves to a targets file, rewrite the `.csproj` setup section in all four places.

## Done when
- [ ] All four demos fixed
- [ ] Docs updated
