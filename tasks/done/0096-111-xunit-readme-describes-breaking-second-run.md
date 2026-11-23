---
type: docs
priority: medium
created: 2026-11-23
closed: 2026-11-23
---
# The README of `DbLive.xunit` describes the removed second run of the breaking scripts

## Release note
None. A correction of the README of `DbLive.xunit` on NuGet; DbLive behaves as before.

## Problem
`src/DbLive.xunit/README.md:6`, the page of the package on NuGet, says of `[SqlDeployFact]`: "It runs every breaking script a second time and compares the schema: a second run must not change it."

0096-53 removed the second run of the migration breaking scripts. `DeploymentTest` (`src/DbLive.xunit/Deploy/DeploymentTest.cs:76-78`) runs `DeployParameters.Breaking` a second time: the migration breaking scripts are applied by then and do not run, and only the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy run again (`docs/llms-full.txt` §13.3). The "Docs to update" of 0096-53 did not list this README, and 0096-98 fixed only `docs/llms-full.txt`.

The README tells users that every migration breaking script must survive a second run, which is the rule 0096-53 dropped.

## Fix
"It runs the breaking phase a second time (the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy run again) and compares the schema: the second run must not change it."

## Docs to update
- `src/DbLive.xunit/README.md`

## Done when
- [x] README fixed
- [x] The other package READMEs searched for "second time" and "breaking script"

## Notes
- The search also covered "second", "twice" and "again" in `src/*/README.md` and the root `README.md`. The README of `DbLive.Templates` says "the release, then the breaking phase twice", which is right. The root `README.md:310` mentions breaking scripts only in the order of the deployment phases.

## Result
`src/DbLive.xunit/README.md`: the line of `[SqlDeployFact]` says that the second breaking phase runs the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy again, that the migration breaking scripts are applied by then and do not run, and that the schema must not change. The first sentence already names the three deployments, so the fix does not repeat "a second time". No other README changed. No code change, no tests.
