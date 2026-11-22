---
type: chore
priority: low
created: 2026-11-17
closed: 2026-11-22
---
# `MigrationChecksumValidator` checks for the `dblive` tables, which always exist when it runs

## Release note
None. One query less per deployment; no change in behavior.

## Problem
`MigrationChecksumValidator.ValidateAsync` starts with `if (!await da.DbLiveInstalledAsync()) return;`. Its only caller is `DbLiveDeployer`, which runs after the self-deployment, so the `dblive` tables always exist there. The check never returns, costs one query per deployment, and suggests that the validator can run on a database without the tables. `AppliedMigrationsValidator`, which runs right before it, reads the same tables without such a check.

The unit test `ValidateAsync_skips_when_dblive_not_installed` covers only this branch.

Found by the second code review of 2026-11-17 (finding 13).

## Where
- `src/DbLive/Deployers/Migrations/MigrationChecksumValidator.cs:13-16`
- `src/DbLive/Deployers/DbLiveDeployer.cs:35` (the only call)
- `src/DbLive.Tests/Deployers/Migrations/MigrationChecksumValidatorTests.cs:6-16` (the test of the branch), and `DbLiveInstalledAsync().Returns(true)` in the other tests of the class

## Fix
Remove the check, its test and the `DbLiveInstalledAsync().Returns(true)` setups of the other tests. Nothing changes for users.

## Done when
- [x] The check and its test removed; the unit tests pass

## Notes
- The check was dead from the start, not since 0096-65: when it came with #93 (803722a), `DbLive.DeployAsync` already ran the self-deployer before `DbLiveDeployer`, the only caller of the validator.
- The other production callers of `DbLiveInstalledAsync` need it: `ProjectIdValidator.ValidateAsync` runs before the self-deployment, `DbLiveSelfDeployer` decides by it whether the tables are created, and `DbLiveTester.RunAllTestsAsync` runs without a deployment.
- `MigrationsDeployerTests` set up `DbLiveInstalledAsync().Returns(true)` four times and `GetSelfMigrationVersionAsync().Returns(1)` once, though `MigrationsDeployer` calls neither. Removed in the same change.

## Result
`MigrationChecksumValidator.ValidateAsync` no longer calls `DbLiveInstalledAsync`. Removed the unit test `ValidateAsync_skips_when_dblive_not_installed`, the `DbLiveInstalledAsync().Returns(true)` setups of `MigrationChecksumValidatorTests` and `MigrationsDeployerTests`, and the unused `GetSelfMigrationVersionAsync` setup of `MigrationsDeployerTests.DeployMigrations_DoNotDeployMigrations`. No docs describe the check. `DbLive.Tests`: 550 passed.
