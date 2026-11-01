---
created: 2026-11-01
---
# Warn about files DbLive never reads

## Idea
A file outside the known folders of the scripts root (`Migrations`, `Code`, `Tests`, `BeforeDeploy`, `AfterDeploy`, or the names set in `settings.json`) is ignored without a word. The AdventureWorks demo kept its post-deployment scripts in `PostDeployment` and they never ran (0096-16). A warning at the start of the deployment would have shown it: "File `PostDeployment/query-store.sql` is in no DbLive folder and is ignored."

The project already has the full file list (`IScriptSource.GetFiles`), so the check is cheap.

Came up while fixing 0096-16.

## Open questions
- [ ] Warning or error? An error can't be missed, but it breaks projects that keep a `README.md` or notes next to the scripts.
- [ ] Which root files are expected: `settings.json` only, or any non-`.sql` file?
- [ ] Also warn about files inside known folders that match no pattern (`Code/notes.txt`, `Migrations/001.sql.bak`)? Same kind of silent skip, but more noise.
- [ ] A setting to silence it (a list of ignored paths), or none until someone asks?
