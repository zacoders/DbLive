---
type: chore
priority: medium
created: 2026-11-07
closed: 2026-11-07
---
# Remove the second run of migration breaking scripts from the deployment test

## Release note
None. The second run came in this release (0096-15, 0096-32, 0096-46) and is removed before it ships; the release notes of those tasks no longer mention it.

## Problem
`[SqlDeployFact]` ran the breaking script of every migration twice in its first breaking deployment (`DeployParameters.CheckBreakingSecondRun`) and failed with `SchemaCheck.BreakingSecondRun` when the second run changed the schema. The reason given was a breaking script with `TransactionWrapLevel: None` that fails halfway and runs again on the next deployment. But the check ran every breaking script twice, also those in a transaction, which are rolled back as a whole on failure. So `alter table dbo.Users drop column Phone;` with the default `TransactionWrapLevel` failed the test on its second run, and users had to write `if exists` into every breaking script.

Found by the code review of 2026-11-07 (finding 7).

## Decision
2026-11-07, maintainer: remove the second run altogether. It gives little now; it was needed for the undo scripts, where the cycle was more complex, and for testing a release.

## Where
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs` (`DeploySecondRunAsync`), `src/DbLive/DeployParameters.cs` (`CheckBreakingSecondRun`), `src/DbLive/Schema/SchemaCheck.cs` (`BreakingSecondRun`; the enum is left with one value and is removed, with `SchemaChangedException.Check`), `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs`.

## Docs to update
- `docs/llms-full.txt` §5.3, §13.3 (the deployment test and the schema check), §17 (`SchemaChangedException`), §19
- `docs/unit-testing/deployment-tests.md`, `docs/unit-testing/index.md`, `docs/deployment/breaking-changes.md` ("In tests"), `docs/deployment/index.md`, `docs/features/index.md`, `docs/llms.txt`
- The comment of the deployment test in the template and the demos

## Done when
- [x] The second run removed, tests updated
- [x] Docs and the release notes of 0096-15, 0096-32 and 0096-46 updated
- [x] `SchemaCheck` and `SchemaChangedException.Check` removed: the enum was left with one value

## Result
- `BreakingChangesDeployer` runs each migration breaking script once; `DeployParameters.CheckBreakingSecondRun` and `SchemaCheck.BreakingSecondRun` are gone. The deployment test still runs the breaking phase twice and fails with `SchemaChangedException` when the second breaking deployment changes the schema: the breaking scripts of the `Code` folder and BeforeDeploy/AfterDeploy run again in it.
- 2026-11-07, maintainer: `SchemaCheck` is removed, it was left with one value (`BreakingSecondDeployment`) and told nothing; `SchemaChangedException` has no `Check` property. Both came in this release, so nothing changes for users of 1.5.0.
- The docs say that the deployment test does not test a migration breaking script with `TransactionWrapLevel: None` that failed halfway; the advice to make such scripts tolerant (`if exists`) stays. The example of a script whose second run drops more is now a breaking script of the `Code` folder.
- Tests: the unit tests of the second run are removed, `Each_breaking_script_runs_once` stays. `SchemaCheckTests` on both providers: `Breaking_script_runs_once` deploys a breaking script without `if exists`, which failed the second run; the tests of `BreakingSecondRun` and of the snapshot inside the deployment transaction are removed (the remaining check reads the schema between deployments).
