---
type: bug
priority: low
created: 2026-10-31
---
# AdventureWorks `PostDeployment` scripts never run

## Problem
`src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/PostDeployment` holds `query-store.sql` (enables Query Store) and `documentaion.sql` (extended properties). DbLive only knows the folders `BeforeDeploy` and `AfterDeploy` (settings `BeforeDeployFolder`, `AfterDeployFolder`), and the demo's `settings.json` does not rename them. The folder is copied to the output and silently ignored: an unknown folder in `Scripts` is not an error.

Verified by reading the code (`DbLiveProject.GetFolderItemsAsync`, `DbLiveSettings.AfterDeployFolder`); not run.

## Where
- `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/PostDeployment/`
- `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/settings.json`

## Fix
- [ ] Rename the folder to `AfterDeploy`, or set `"AfterDeployFolder": "PostDeployment"`.
- [ ] AfterDeploy runs on every deployment, so both scripts must be idempotent. `documentaion.sql` calls `sp_addextendedproperty`, which fails when the property already exists: the second deployment would fail. Switch to "add if missing, else update", or move it to a migration.
- [ ] Rename `documentaion.sql` → `documentation.sql`.
- [ ] Should DbLive warn about unknown folders in the scripts root? It would have caught this.

## Done when
- [ ] Scripts run on every deployment without errors, AdventureWorks tests green
