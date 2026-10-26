---
type: bug
priority: high
created: 2026-10-26
closed: 2026-10-26
---
# Deploy fails without a `Migrations` folder

## Problem
Without a `Migrations` folder the deploy failed with `DirectoryNotFoundException`. `Code`, `Tests`, `BeforeDeploy` and `AfterDeploy` are checked with `PathExists`, `Migrations` was not. An empty folder is not copied to the output, so a code-only project could not be deployed. Verified by an experiment.

## Where
`src/DbLive/Project/DbLiveProject.cs:85-87` (`GetMigrationsAsync`).

## Fix
Return an empty list when the folder is missing, as `GetCodeGroupsAsync` does. Add a unit test.

## Docs to update
- `docs/llms-full.txt` §0 (rule 9), §4 (folder table), §17 (`DirectoryNotFoundException` row)
- `docs/project/structure.md` (warning "The Migrations folder must exist" and the Required column)
- `docs/gettingstarted.md` (sentence about Migrations in step 3)

## Result
Fixed in ab5fe38: `GetMigrationsAsync` returns an empty list when the folder does not exist. Test: `MigrationsTests`. Docs updated in the same commit.
