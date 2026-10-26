---
type: bug
priority: low
created: 2026-10-26
---
# Nested paths in `CodeSubFoldersDeploymentOrder` work only on Windows

## Problem
The path from the settings is compared using the OS separator. `"dbo\\functions"` doesn't match on Linux and `"dbo/functions"` doesn't match on Windows. The group silently ends up empty and the deployment order breaks. `DemoMSSQL/Scripts/settings.json` uses `"dbo\\functions"`.

## Where
`src/DbLive/Project/DbLiveProject.cs:46`.

## Fix
Normalize `\` and `/` to `Path.DirectorySeparatorChar` before comparing. Also log a warning when a group from the settings matches no files.

## Docs to update
- `docs/llms-full.txt` §6, §19
- `docs/project/configuration.md` (paragraph after the Folders table)

## Done when
- [ ] Separators normalized, with unit tests for both separators
- [ ] Warning for an empty group
- [ ] Docs updated
