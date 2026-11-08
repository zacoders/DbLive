---
type: bug
priority: low
created: 2026-11-09
---
# The breaking guard passes after a `Default` run that failed before the code phase

## Problem
The case needs an operator mistake: the `Breaking` stage started although the `Default` run of the same build failed or did not run. A pipeline does not start it then, and DbLive does not guard against operator mistakes (decision on 0096-41, 2026-11-07). But this is the case the guard exists for, and the docs promise that it catches it:

- the release note of 0096-15: "a `Breaking` run after a failed or missing `Default` run of the same build no longer drops anything";
- `docs/llms-full.txt` §5.5: "It catches: ... and `Breaking` run before `Default`";
- §17, `CodeNotDeployedException`: "the `Default` run of this build failed or did not run yet";
- §18.10, step 6: "It fails with `CodeNotDeployedException` if step 3 did not complete for this build";
- the comment of `ValidateCodeDeployedAsync`: "the Default run failed, Breaking before Default".

The guard checks only code files, so two cases pass it (from the code, not verified by a test):

1. **The `Default` run failed in the migrations, and the build changed no code file.** Build N+1 has migration 10 (a new column), `10.breaking.sql` (drops the old column) and migration 11. `Default` with `TransactionWrapLevel: Migration`: 10 is committed, 11 fails, the code phase does not run. Then `DeployParameters.Breaking`: every code file has an `applied` row with the same hash, the guard passes, and `DeployMigrationBreakingAsync` runs `10.breaking.sql`, since it takes every applied migration version. The old column is dropped while application N runs.
2. **`Breaking` before `Default` of a release that only deletes code files.** N+1 deletes `GetUser_v1.sql` and adds `GetUser_v1.breaking.sql`. The remaining code files are `applied` with the hashes of N, the guard passes, and the breaking script drops `dbo.GetUser_v1`, which application N calls. Catching this needs the check proposed in 0096-41 (rows of code files that the build does not have), which was rejected.

Found by the code review of 2026-11-09 (finding 3).

## Where
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs:38-67` (`ValidateCodeDeployedAsync` and its comment)
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs:69-87` (`DeployMigrationBreakingAsync`)

## Fix
- [ ] Fix the guard, or correct only the docs? Following the decision on 0096-41, the default is the docs.
  - Guard: also fail when a migration version of the project is not applied (`IDbLiveDA.GetAppliedMigrationVersionsAsync`, one query). Covers case 1, and `Breaking` before `Default` of a release with a new migration. Case 2 stays.
  - Docs: the guard catches a `Default` run that failed in the code phase. A `Default` run that failed earlier, or did not run, is caught only when the build changed the content of a code file.

## Docs to update
- the release note of `tasks/done/0096-15-drop-old-procedure-version.md`
- `docs/llms-full.txt` §5.5, §17 (`CodeNotDeployedException`), §18.10 (step 6)
- `docs/deployment/breaking-changes.md`
- the comment of `ValidateCodeDeployedAsync`

## Done when
- [ ] Decided
- [ ] The guard fixed, with a test of case 1 on SQL Server and PostgreSQL; or the docs and the comment corrected
