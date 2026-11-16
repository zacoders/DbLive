---
created: 2026-11-16
---
# A large generated project to test deployment at scale

## Idea
A generator of DbLive projects of a given size and shape, so that a large project can be deployed and measured now, without waiting for a user to have one. The generator lives in the repository; the generated files do not.

The largest project in the repository is the AdventureWorks demo: 68 SQL files, 52 of them code. Demo.PostgreSQL.Chinook has 10 files (about 16 000 lines, mostly data). They test the behavior, not the scale.

Came up 2026-11-16, during #96.

## Why
- 0096-44 changed the code deployment for large projects without measuring it. It now reads `dblive.code` with one query instead of a round trip per file, and `MarkMissingCodeAsRemovedAsync` uses a TVP. The task says "Not measured" and "Not verified by a plan". The maintainer dropped the measurement because reading files from a local NVMe disk tells nothing, and that still holds. A large project would measure the cost on the database side: round trips, and the plan of a query over thousands of rows.
- The code deployment retries failed files in rounds (`CodeDeployer.DeployGroupAsync`). A group of N files takes at most N rounds, and the files of a round are taken in the order of their relative path. Take a chain of views where each view uses the next one in name order: `v_a` uses `v_b`, `v_b` uses `v_c`. It deploys one view per round, so a chain of length D executes D(D+1)/2 files. Not verified; with 52 code files it does not show. Today the user's way around it is `CodeSubFoldersDeploymentOrder`.
- Other costs grow with the project and are not measured: the number of applied migrations (the applied versions check, checksum validation), SQL tests (a transaction per test), the schema snapshot of the deployment test, the breaking phase, downgrade.

## Shape of the generated project
The generator takes these parameters, plus a seed, so that the same parameters give the same files:

- tables in several schemas, with foreign keys;
- code: views on tables and on other views (the depth of dependency chains), inline and scalar functions, procedures that call procedures, triggers, long procedures, files with several batches (`GO` on SQL Server);
- for part of the chains, a name order that runs against the dependency order (the worst case of the rounds);
- migrations: their number, some with breaking and undo scripts;
- SQL tests: their number, some with `setup.test.sql`;
- both providers.

Realism matters more than size: 10 000 copies of `select 1` find nothing that a real project hits.

## Scenarios
| Scenario | What it shows |
|---|---|
| First deployment to an empty database | all migrations and all code; the rounds of retries |
| Deploy again, nothing changed | the most frequent deployment in CI; what 0096-44 optimized |
| One code file changed | the cost of a small release |
| One new migration on a database with hundreds applied | the applied versions check and checksums |
| Breaking phase | the guard over all code files |
| All SQL tests | the cost per test |
| Downgrade by several versions | undo scripts |
| Deployment test with the schema check | the snapshot of a large schema |

Run each scenario with 1 and 10 threads (`NumberOfThreadsForCodeDeploy`), and with `TransactionWrapLevel.Deployment`, which uses one thread.

## What to measure
- Wall time per phase. Noisy on shared CI runners, so compare on one machine, before and after a change.
- Commands sent to the database, and rounds of the code deployment. Both are deterministic, so a test can fail on a regression.
- Memory of the process: the project is read once per builder and kept (0096-44).

## Where to run
Not on every pull request: it is slow, and the time is noisy. A local command with Testcontainers, like the integration tests, and maybe a workflow started by hand.

## Open questions
- [ ] A generator, a real large schema (WideWorldImporters, or an open-source project with thousands of objects), or both: the real schema for the shape, the generator for the size?
- [ ] What size counts as large: 3 000 code files (the number in 0096-44), 10 000, 20 000? How large are the projects of users?
- [ ] Where does the generator live: a file-based app in `.github/scripts/`, like `release-notes.cs`, or a project under `src/`?
- [ ] How are the commands counted: a wrapper over `IDbLiveDA` or the connection, or on the server (Query Store, `pg_stat_statements`)?
- [ ] Assert the counts in tests (fail when a deployment with nothing changed makes more than K round trips), or keep them as numbers in the Notes of a task?
- [ ] Start small, with the generator, one run of each scenario and the numbers in Notes, and automate only what turns out to be worth watching?
- [ ] If the measurement confirms the quadratic case of the rounds: order the files by their dependencies (by parsing the names a file uses?), or document `CodeSubFoldersDeploymentOrder` for large projects?

## Sources
- `tasks/done/0096-44-project-files-read-many-times-per-deploy.md`
- `src/DbLive/Deployers/Code/CodeDeployer.cs` (`DeployGroupAsync`)
