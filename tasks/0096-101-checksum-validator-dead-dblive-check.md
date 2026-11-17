---
type: chore
priority: low
created: 2026-11-17
---
# `MigrationChecksumValidator` checks for the `dblive` tables, which always exist when it runs

## Problem
`MigrationChecksumValidator.ValidateAsync` starts with `if (!await da.DbLiveInstalledAsync()) return;`. Its only caller is `DbLiveDeployer`, which runs after the self-deployment (0096-65), so the `dblive` tables always exist there. The check never returns, costs one query per deployment, and suggests that the validator can run on a database without the tables. `AppliedMigrationsValidator`, which runs right before it, reads the same tables without such a check.

The unit test `ValidateAsync_skips_when_dblive_not_installed` covers only this branch.

Found by the second code review of 2026-11-17 (finding 13).

## Where
- `src/DbLive/Deployers/Migrations/MigrationChecksumValidator.cs:13-16`
- `src/DbLive/Deployers/DbLiveDeployer.cs:35` (the only call)
- `src/DbLive.Tests/Deployers/Migrations/MigrationChecksumValidatorTests.cs:6-16` (the test of the branch), and `DbLiveInstalledAsync().Returns(true)` in the other tests of the class

## Fix
Remove the check, its test and the `DbLiveInstalledAsync().Returns(true)` setups of the other tests. Nothing changes for users.

## Done when
- [ ] The check and its test removed; the unit tests pass
