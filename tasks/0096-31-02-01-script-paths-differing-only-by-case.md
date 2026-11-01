---
type: bug
priority: low
created: 2026-11-01
---
# Two scripts whose paths differ only by case share one row in `dblive`

## Problem
Since 0096-31-02 `relative_path` is stored in lower case. On Linux a project may have two files whose paths differ only by case (`Code/dbo/GetUser.sql` and `Code/dbo/getuser.sql`; `DbLiveProject.GetFolderFiles` sorts them with a second `StringComparer.Ordinal` key for this case). Both map to one row:
- Code: each file finds the hash of the other one in `dblive.code`, so both are redeployed on every deployment. With parallel code deploy they may also save the same row at the same time (the SQL Server `merge` has no `holdlock`, so two inserts of a new row can fail with a primary key violation).
- Tests and BeforeDeploy/AfterDeploy: the two files overwrite each other's last result.

Such a checkout cannot exist on Windows or macOS (case-insensitive file systems), so the project is not portable anyway.

## Fix
- [ ] Fail early when two script paths differ only by case: an exception that names both paths, thrown while reading the project (`DbLiveProject` or the script source), for code, tests and folder items. Migrations are identified by version and already fail on a duplicate version.
- [ ] Unit test with `InMemoryScriptSource`.

## Docs to update
- `docs/llms-full.txt` §4 (discovery rules) and §17 (exceptions)

## Done when
- [ ] Two scripts whose paths differ only by case stop the deployment with a clear message, with a unit test
- [ ] Docs updated
