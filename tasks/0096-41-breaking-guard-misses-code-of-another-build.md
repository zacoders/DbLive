---
type: bug
priority: high
created: 2026-11-05
---
# The breaking phase guard does not notice the code of another build

## Problem
`BreakingChangesDeployer.ValidateCodeDeployedAsync` checks only that every code file of this build has an `applied` row with its current hash. It does not notice that the database has code of another build. So a stale `Breaking` job of build N+1 after a rollback to N passes the guard when N+1 only deleted code files, and it drops what application N uses.

Scenario (from the code, not verified by a test):

1. Build N has `Code/dbo/GetUser_v1.sql`.
2. Build N+1 does what §6.1 recommends: it deletes the file and adds `GetUser_v1.breaking.sql` with `drop procedure if exists dbo.GetUser_v1`. The other code files do not change.
3. N+1 is deployed (`Default`): the row of `getuser_v1.sql` becomes `removed`.
4. Rollback to N: `GetUser_v1.sql` is executed again, its row is `applied`.
5. The stale `DeployParameters.Breaking` job of N+1 runs. Every code file of N+1 is `applied` with the same hash, the guard passes, and `CodeBreakingDeployer` drops `dbo.GetUser_v1`, which the deployed build N declares.

The docs promise that this case fails with `CodeNotDeployedException`: `docs/llms-full.txt` §5.6 ("a stale `Breaking` job of N+1 after the database was rolled back to N") and §18.8 ("Do not run a breaking deployment of the newer build afterwards: it fails with `CodeNotDeployedException`"), and the release note of 0096-15 ("or after a rollback, no longer drops anything"). It holds only when N+1 also changed the content of a code file.

Two smaller problems of the same guard:

- It reads and hashes all code files again (`GetCodeGroupsAsync`) and runs one `FindCodeItemAsync` per file, one after another: a project with 3000 code files makes 3000 round trips on every breaking deployment.
- The "is deployed" check is copied from `CodeItemDeployer` (`row != null && Status == Applied && MatchesHash`), which the comment at line 54 says. The two copies can diverge.

## Where
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs:44` (`ValidateCodeDeployedAsync`)
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:21` (the same check)
- `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs` (a query for all rows of `dblive.code`)

## Fix
- [ ] The guard also fails when `dblive.code` has an `applied` row with `item_type = 'code'` whose path is not a code file of this build. In the normal flow the `Default` run of the same build has marked such rows `removed`, so the guard passes.
- [ ] One query for `relative_path`, `item_type`, `status`, `content_hash` of all rows of `dblive.code`, compared in memory, instead of one query per file.
- [ ] One "is deployed" check for `CodeItemDeployer` and the guard, for example `CodeItemDto.IsDeployed(FileData)`.
- [ ] The message for the new case: the same `CodeNotDeployedException` with another text ("the database has code files this build does not have: ..., it was rolled back to another build"), or a new exception?
- [ ] Without a `ProjectId`, two projects deployed to one database: project B deployed between the `Default` and the `Breaking` run of project A now fails A's guard. Acceptable? Before, A's `Default` run marked B's rows `removed` anyway (§6).

## Docs to update
- `docs/llms-full.txt` §5.6 (guard), §12 step 13, §17 (`CodeNotDeployedException`), §18.8
- `docs/deployment/breaking-changes.md`
- `docs/deployment/zero-downtime.md`, if it names the cases of the guard (the maintainer's page: ask before rewriting)

## Done when
- [ ] Fixed, with a test of the scenario above on SQL Server and PostgreSQL
- [ ] The guard makes one query, with a unit test
- [ ] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96). Merges three findings of the review: the missed case, the round trip per file, the copied check.

2026-11-06, rechecked against 0096-46 (undo scripts and the downgrade are removed): the scenario stays possible. N+1 in it is a code-only release (it deletes a code file and adds a breaking script), so deploying the build of N over it is a plain deployment: no migration version is missing from the build, and nothing stops it. A second case appears with the rollback of 0096-46, which does not deploy N at all: the application is rolled back to N and the database stays at N+1. A `Breaking` job of N+1 that runs then drops `GetUser_v1`, which application N calls, and no guard can see it: the database is exactly what N+1 deployed. This task covers the first case; for the second, §18.8 must say that the breaking phase of a release whose application was rolled back never runs (0096-46).
