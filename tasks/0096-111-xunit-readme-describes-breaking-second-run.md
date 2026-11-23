---
type: docs
priority: medium
created: 2026-11-23
---
# The README of `DbLive.xunit` describes the removed second run of the breaking scripts

## Problem
`src/DbLive.xunit/README.md:6`, the page of the package on NuGet, says of `[SqlDeployFact]`: "It runs every breaking script a second time and compares the schema: a second run must not change it."

0096-53 removed the second run of the migration breaking scripts. `DeploymentTest` (`src/DbLive.xunit/Deploy/DeploymentTest.cs:76-78`) runs `DeployParameters.Breaking` a second time: the migration breaking scripts are applied by then and do not run, and only the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy run again (`docs/llms-full.txt` §13.3). The "Docs to update" of 0096-53 did not list this README, and 0096-98 fixed only `docs/llms-full.txt`.

The README tells users that every migration breaking script must survive a second run, which is the rule 0096-53 dropped.

## Fix
"It runs the breaking phase a second time (the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy run again) and compares the schema: the second run must not change it."

## Docs to update
- `src/DbLive.xunit/README.md`

## Done when
- [ ] README fixed
- [ ] The other package READMEs searched for "second time" and "breaking script"
