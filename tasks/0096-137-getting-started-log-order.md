---
type: docs
priority: low
created: 2026-11-23
---
# The sample log of Getting Started has two lines in the wrong order

## Problem
`docs/gettingstarted.md:141-142` shows `Starting project deploy from MyDb.dll.` and then `ProjectId bound to database: my-app-db.`. `DbLive.DeployAsync` binds the ProjectId (`src/DbLive/DbLive.cs:54`, logged in `ProjectIdValidator.cs:63`) before it calls `DbLiveDeployer.DeployAsync`, which logs `Starting project deploy from ...` (`src/DbLive/Deployers/DbLiveDeployer.cs:21`). `docs/llms-full.txt` §3.4 agrees with the code.

## Fix
Swap the two lines; better, copy the log of a real run of the template.

## Docs to update
- `docs/gettingstarted.md`

## Done when
- [ ] Docs updated
