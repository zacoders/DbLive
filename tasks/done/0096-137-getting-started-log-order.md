---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The sample log of Getting Started has two lines in the wrong order

## Release note
None. A correction of `docs/gettingstarted.md`; DbLive behaves as before.

## Problem
`docs/gettingstarted.md:141-142` shows `Starting project deploy from MyDb.dll.` and then `ProjectId bound to database: my-app-db.`. `DbLive.DeployAsync` binds the ProjectId (`src/DbLive/DbLive.cs:54`, logged in `ProjectIdValidator.cs:63`) before it calls `DbLiveDeployer.DeployAsync`, which logs `Starting project deploy from ...` (`src/DbLive/Deployers/DbLiveDeployer.cs:21`). `docs/llms-full.txt` §3.4 agrees with the code.

## Fix
Swap the two lines; better, copy the log of a real run of the template.

## Docs to update
- `docs/gettingstarted.md`

## Done when
- [x] Docs updated

## Notes
- The rest of the sample matches the code, checked against every `Information` call of a `Default` run with `CreateDbIfNotExists`: the self-deployment logs only with `LogSelfDeploy`, the breaking phase logs nothing when `DeployBreaking` is false, `Deploying code.` and `Code deploy successfully completed.` with no code file between them. So the lines were swapped, not replaced by the log of a real run.

## Result
`docs/gettingstarted.md`, the sample log: `ProjectId bound to database: my-app-db.` before `Starting project deploy from MyDb.dll.`, as `DbLive.DeployAsync` runs them. No code change, no tests.
