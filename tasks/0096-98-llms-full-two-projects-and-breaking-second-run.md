---
type: docs
priority: medium
created: 2026-11-17
---
# `docs/llms-full.txt`: two projects in one database, and a second run of migration breaking scripts

## Problem
Three lines of `docs/llms-full.txt` describe behavior that a task of 2.0 changed:

- **Two projects in one database.** §6 (line 330) says that without a `ProjectId` two projects deployed to one database mark each other's code rows `removed`, and the other project then redeploys all its code: "slower, harmless". §12.1 (line 858) says that such projects deploy one at a time. Since 0096-46 the applied versions check (`AppliedMigrationsValidator`) stops a deployment whose project does not have every applied migration version. Project B fails with `DatabaseAheadOfProjectException` that lists the versions of project A, before any script runs, and A fails the same way once B has applied a migration of its own. Two projects with migrations cannot share a database, so the case that the docs call harmless does not happen. From the code, not run.
- **Both runs of a migration breaking script.** §10 (line 709), the row of `Migration`, says that the transaction applies "to both runs of a migration breaking script in `[SqlDeployFact]`". 0096-53 removed the second run, and §5.3 (line 295) says that the deployment test runs the breaking script of every migration once. 0096-53 did not list §10 under its docs to update.

An agent that reads §6 can advise deploying two projects into one database without a `ProjectId`.

The pages under `docs/` do not repeat these statements: a search for "two projects", "one database" and "both runs" on 2026-11-17 found only correct ones.

Found by the second code review of 2026-11-17 (findings 7 and 8).

## Where
- `docs/llms-full.txt:330` (§6), `:858` (§12.1), `:709` (§10)
- `src/DbLive/Deployers/Migrations/AppliedMigrationsValidator.cs`

## Fix
- §6: drop the sentence about two projects, or say that the applied versions check stops a project deployed to a database where another project applied migrations.
- §12.1: drop "two projects in one database (no `ProjectId`) deploy one at a time".
- §10: "and to the run of a migration breaking script in `[SqlDeployFact]`", or drop the clause, since §5.3 says it.
- [ ] Say in §14 that a database holds one project? Rule 17 and §14 treat the database of another project as the wrong database, but no line says that two projects cannot share one.

## Docs to update
- `docs/llms-full.txt` §6, §10, §12.1, maybe §14

## Done when
- [ ] The three lines corrected
