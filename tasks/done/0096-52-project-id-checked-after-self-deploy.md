---
type: bug
priority: medium
created: 2026-11-07
closed: 2026-11-07
---
# ProjectId is checked after the self-deployment

## Release note
**ProjectId is checked before DbLive changes its own tables.** A deployment to the database of another project fails with `ProjectIdMismatchException` before the self-deployment, so the deployer of one project pointed at the database of another one by mistake no longer upgrades the `dblive` tables there. A database without a ProjectId gets it right after the self-deployment, as before.

## Problem
`DbLive.DeployAsync` ran the self-deployment first, and `DbLiveDeployer` checked the ProjectId after it. The self-migrations of 2.0 rename stored values, convert paths and drop data of the `dblive` tables, and the self-deployment writes `min_dblive_lib_version`. So a deployment of project A to the database of project B, which an older DbLive deploys, upgraded the `dblive` tables of B and only then failed with `ProjectIdMismatchException`; the next deployment of B with its older DbLive could not read them (verified 2026-11-07: with the old order the new test sees `dblive.version.version` go from 11 to 13 before the exception).

It needs an operator mistake (a wrong connection string), but that mistake is exactly what ProjectId exists for.

Found by the code review of 2026-11-07 (finding 5).

## Where
- `src/DbLive/DbLive.cs`: the order of the self-deployment and the project deployment.
- `src/DbLive/Deployers/DbLiveDeployer.cs`: the ProjectId check at its start.
- `src/DbLive/Deployers/ProjectIdValidator.cs`: the check and the binding were one step.

## Fix
- `IProjectIdValidator.ValidateAsync` only compares and runs in `DbLive.DeployAsync` before the self-deployment. A database without the DbLive tables or without a ProjectId passes.
- `IProjectIdValidator.BindAsync` stores the ProjectId in a database that has none, right after the self-deployment, which creates the `dblive` tables of a new database. It compares again, so a value stored in between is not overwritten.
- `GetProjectIdAsync` of both providers returns null when the `project_id` column does not exist yet (a database deployed before self-migration 6 on SQL Server, 3 on PostgreSQL), like `GetMinDbLiveLibVersionAsync`: it is now read before the self-deployment adds the column.

## Docs to update
- `docs/llms-full.txt` §11.3 (what a deployment still performs), §12 (order), §14
- `docs/features/project-id.md`, `docs/deployment/index.md` ("What a deployment does")

## Done when
- [x] Fixed, with unit tests and integration tests on both providers
- [x] Docs updated

## Result
- `DbLive.DeployAsync`: `ValidateAsync`, the self-deployment, `BindAsync`, then the project deployment. `DbLiveDeployer` no longer takes `IProjectIdValidator`. `ProjectIdValidator` has one helper for the settings value and one for the mismatch; the warning for a missing ProjectId is logged once, by `ValidateAsync`.
- `MsSqlDA.GetProjectIdAsync` reads the column only when `col_length` finds it; `PostgreSqlDA.GetProjectIdAsync` reads it through `to_jsonb`.
- Tests: unit (`ProjectIdValidatorTests`, `DbLiveTests`: the order, and no self-deployment after a mismatch); integration `ProjectIdTests` on both providers: a database of another project in the state before the last self-migration keeps its `dblive` tables unchanged (failed with the old order), and `GetProjectIdAsync` returns null without the column.
