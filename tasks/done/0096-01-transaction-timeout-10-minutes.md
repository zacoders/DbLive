---
type: bug
priority: high
created: 2026-10-26
closed: 2026-10-26
---
# Transactions abort after 10 minutes

## Problem
.NET caps every `TransactionScope` timeout at `TransactionManager.MaximumTimeout`, which is 10 minutes by default. So `MigrationTimeout` (12 h), `DowngradeTimeout` (12 h) and `DeploymentTimeout` (1 day) had no effect: a transactional migration longer than 10 minutes failed with `TransactionAbortedException`. Verified by an experiment.

## Where
`src/DbLive/Common/TransactionScopeManager.cs`, method `Create`.

## Fix
Raise `TransactionManager.MaximumTimeout` to the largest configured timeout before deploying. Alternative without a process-wide side effect: compare the timeouts with the cap at startup and fail with a clear error.

## Docs to update
- `docs/llms-full.txt` §10 ("10-minute cap"), §17 (`TransactionAbortedException` row), §19
- `docs/project/configuration.md` (note "Transactions longer than 10 minutes")
- `docs/deployment/migrations.md` ("Long-running migrations")

## Result
Fixed in 42903f7. `TransactionScopeManager.Create` calls `EnsureMaximumTimeout`, which raises `TransactionManager.MaximumTimeout` to the scope's timeout and never lowers it; `TimeSpan.Zero` means no limit. Side effect, documented: other `TransactionScope`s in the same process are afterwards capped at the largest timeout DbLive used. Tests: `TransactionScopeManagerTests`. Docs updated in the same commit.
