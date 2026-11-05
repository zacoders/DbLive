---
type: bug
priority: medium
created: 2026-11-05
---
# An older DbLive deploys a database that a newer DbLive has deployed

## Problem
DbLive changes its own schema `dblive` with self-migrations and keeps their version in `dblive.version`. `DbLiveSelfDeployer` applies the self-migrations above that version, and does nothing when the stored version is above its own last self-migration. A build with an older DbLive then reads and writes the `dblive` tables of a newer one: a value it cannot parse fails with an error that does not say why (a Dapper enum parse), and a value it parses but understands differently goes unnoticed.

After 0096-46 there is no downgrade, but an older DbLive still meets such a database when an old artifact is deployed: by mistake, or as a rollback after a release that only upgraded DbLive. Such a release has no migrations of its own, so the check of 0096-46 (the database has applied versions the build does not have) does not stop it.

## Where
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs:25` (reads `dblive.version` and filters the self-migrations)

## Fix
- [ ] When `dblive.version` is above the last self-migration of this DbLive, the deployment fails before any change, with a message such as: "The database was deployed by a newer DbLive: its `dblive` schema is version 12, this DbLive knows versions up to 10. Deploy with that DbLive version or a newer one." Exception name: `DbLiveSchemaTooNewException`?
- [ ] `RunAllTestsAsync` writes `dblive.unit_test_run` without a deployment: does it check the version too?

## Docs to update
- `docs/llms-full.txt` §15 (self-deployment of the `dblive` schema), §17 (the new exception), §18.8 (an old artifact deployed after a DbLive upgrade fails: roll back the application instead)
- `docs/deployment/index.md` (the `dblive` schema)

## Done when
- [ ] Implemented, with a unit test
- [ ] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96), first as "after a deployment with DbLive 2.0, a build with DbLive 1.5.0 cannot deploy the database": the self-migrations 07 to 10 (SQL Server) and 04 to 07 (PostgreSQL) write values that 1.5.0 cannot read (`migration.settings`, `breaking.settings`, `undo.settings`, the code status `removed`, paths with `/` in lower case).

2026-11-06, maintainer: versions before 2.0 are not considered, the library has no users; undo scripts and the downgrade are removed (0096-46). The task is reduced to the guard. The options considered for a rollback across a DbLive upgrade are in the Notes of 0096-46.
