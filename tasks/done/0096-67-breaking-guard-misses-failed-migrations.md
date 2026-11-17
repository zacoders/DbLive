---
type: bug
priority: low
created: 2026-11-09
closed: 2026-11-14
---
# The breaking guard passes after a `Default` run that failed before the code phase

## Release note
None. Corrects the docs and the release note of breaking scripts in the `Code` folder, released together with this one: the breaking guard catches a `Default` run that failed in the code phase; one that failed earlier, or did not run, only when the build added or changed a code file.

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
- [x] Fix the guard, or correct only the docs? Following the decision on 0096-41, the default is the docs.
  - Guard: also fail when a migration version of the project is not applied (`IDbLiveDA.GetAppliedMigrationVersionsAsync`, one query). Covers case 1, and `Breaking` before `Default` of a release with a new migration. Case 2 stays.
  - Docs: the guard catches a `Default` run that failed in the code phase. A `Default` run that failed earlier, or did not run, is caught only when the build changed the content of a code file.
  - Decided by the maintainer on 2026-11-14: the docs only. Starting the breaking phase without a successful `Default` run of the same build is an operator mistake and the operator's problem; it cannot be guarded against without making DbLive much more complex.

## Docs to update
- the release note of `tasks/done/0096-15-drop-old-procedure-version.md`
- `docs/llms-full.txt` §5.5, §17 (`CodeNotDeployedException`), §18.10 (step 6)
- `docs/deployment/breaking-changes.md`
- the comment of `ValidateCodeDeployedAsync`

## Done when
- [x] Decided
- [x] The guard fixed, with a test of case 1 on SQL Server and PostgreSQL; or the docs and the comment corrected

## Notes
2026-11-14, case 1 rechecked against the code (not by a test): `MigrationsSaver` writes the rows of every migration item, `10.breaking` with status `none`, on every run, `Breaking` included; the default `TransactionWrapLevel` is `Migration`, so migration 10 stays committed when 11 fails; the guard reads only `dblive.code`; `DeployMigrationBreakingAsync` intersects the applied migrations with the breaking items that are not applied, so `10.breaking.sql` runs. A project without code files has no guard at all (documented).

Options considered on 2026-11-14 and dropped by the maintainer:
- The migration check above: covers case 1, not case 2.
- A "release completed" mark in `dblive.version`, reset at the start of `Default` and set at its end: it does not know which build set it, so `Breaking` before `Default` still passes with the mark of the previous release; the reset must be committed outside the deployment transaction (`TransactionWrapLevel: Deployment` would roll it back); every combination of `DeployParameters` needs a rule.
- A fingerprint of the build (a hash of every file of the project) written by a completed `Default` and compared by `Breaking`: covers cases 1 and 2 and the scenario of 0096-41, but needs a self-migration, transaction handling and rules for partial runs. Too complex for an operator mistake.

A breaking script can reach the breaking phase without a new migration in two ways: a breaking script of the Code folder, and `<version>.breaking.sql` added in a later release to a version that is already applied (the checksum validation compares only applied rows). The second works but is not the DbLive way, decided by the maintainer on 2026-11-14: the breaking script is written together with its migration; added later, it is easy to forget, and by then the change is no longer breaking for the application and can be an ordinary migration. It is not documented, so as not to confuse users. Either way, no cheap check of the database closes `Breaking` before `Default`.

`docs/deployment/zero-downtime.md` (step 6) is left as it is: its example changes a code file (`GetUsers.sql`), so the guard catches a failed or skipped step 3 there.

## Result
Docs only, no change in behavior.
- `docs/llms-full.txt` §5.5: the guard catches a `Default` run that failed in the code phase; one that failed earlier, or did not run, only when the build added or changed a code file; otherwise the breaking phase runs, and DbLive does not guard against this operator mistake.
- §17 (`CodeNotDeployedException`): its absence does not prove that `Default` succeeded.
- §18.10, step 6: run it only after step 3 succeeded; a failed step 3 is caught only when the build added or changed a code file.
- `docs/deployment/breaking-changes.md` ("When breaking scripts run"): the same.
- The release note of 0096-15 and the comment of `BreakingChangesDeployer.ValidateCodeDeployedAsync`: the same.
