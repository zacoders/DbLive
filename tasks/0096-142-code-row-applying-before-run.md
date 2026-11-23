---
type: bug
priority: high
created: 2026-11-23
---
# A code file whose run is not confirmed is skipped when its previous content comes back

## Problem
The row of a code file in `dblive.code` keeps the hash of its last success while the file runs and after it fails (0096-141). The next deployment skips the file as `[hash match]` when its content returns to that hash (`git revert`, a redeploy of the previous release). That is wrong when the run that was not confirmed changed the database:

1. SQL Server, a code file with In-Memory OLTP DDL (`RunsCodeItemWithoutTransaction`): its `GO` batches commit one by one. A later batch fails after an earlier one committed (the `drop` succeeds, the `create` fails): the object is missing or changed in part, and the previous content, which would restore it, is skipped. Today §6 tells the user to change the file or delete its row.
2. Both providers, every code file outside the deployment transaction: the file commits, and the write of its `applied` row fails (the connection breaks, a failover). `CodeItemDeployer` runs the file and writes its row with two commands (`ExecuteCodeItemAsync`, `SaveCodeItemAsync`), not in one transaction. The object is the new version, the row is the old one; when the previous content comes back, the file is skipped and the object stays the new version.

0096-141 decided not to withdraw the last success on failure (its Notes): a write in `catch` is what 0096-51, 0096-60 and 0096-89 were about, and it cannot help in case 2. A mark written before the run covers both cases and writes nothing on failure.

Not needed for the other scripts:
- A migration row is `none` until its script succeeds (`MigrationsSaver`), and only an `applied` version is skipped (`MigrationsDeployer.GetMigrationsToApplyAsync`), with any content: an unconfirmed migration always runs again from the start. With `TransactionWrapLevel: Migration` the script and its status commit together; with `None` the script must be re-runnable (§5.3). An older build that does not have the migration has nothing to run (`DatabaseAheadOfProjectException` counts only `applied` versions); the release that brings it back runs it again.
- BeforeDeploy/AfterDeploy scripts and the breaking scripts of the Code folder run on every deployment.

## Where
- `src/DbLive/Deployers/Code/CodeDeployer.cs` (`SkipDeployedAsync`), `CodeRows.cs`
- `src/DbLive/Adapter/CodeItemStatus.cs`, `IDbLiveDA.cs`
- `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: a command next to `MarkCodeAsVerifiedAsync`
- `src/DbLive.Tests/SelfDeployer/DbLiveTablesSnapshotTests.cs`: a new value of a stored enum
- tests: `CodeFileFailureTests` on both providers (`Failed_code_file_keeps_the_row_of_its_last_success_and_runs_again_on_the_next_deployment`), `CodeFileTransactionTests` (SQL Server, In-Memory OLTP), `CodeIDeployerTests`

## Fix
1. A new status of a `dblive.code` row, `applying` (`CodeItemStatus.Applying`): DbLive started to deploy new content of the file and has not confirmed it.
2. Before the code files run, the files to run that have an `applied` row get `status = 'applying'`, with one command for all of them, as `MarkCodeAsVerifiedAsync` does for the skipped files. `content_hash` and `applied_utc` keep the last success. `CodeRows` already holds the rows: a file without a row or with a `removed` row runs anyway and is not marked, so a first deployment sends no command.
3. A successful run writes the `applied` row, as now. A failed run writes nothing: the row stays `applying`.
4. `CodeRows.IsDeployed` does not change: only an `applied` row with the current hash is deployed, so a file with an `applying` row runs with any content. The guard of the breaking phase sees the file as not deployed, and `MarkMissingCodeAsRemovedAsync` turns the row of a deleted file into `removed`: both right as they are.
5. In the deployment transaction the mark is rolled back with the files on failure and overwritten by `applied` on success, so it changes nothing there. Writing it there too keeps one path.
6. An error of the mark itself fails the deployment before any code file runs, as any error of DbLive does: it is not written in `catch`, so it hides nothing.
7. Compatibility: `applying` is a new stored value ("Compatibility of the `dblive` tables" in `Development.md`). 2.0 is not released and `MinCompatibleVersion` is 2.0.0 already: it stays when this task is in 2.0. After the release it would raise the minimum and block a rollback to 2.0, the case this task is about. No self-migration: `status` is `varchar(32)` without a check on both providers.

## Docs to update
Line numbers as of 2026-11-23.
- `docs/llms-full.txt` §6: line 337 (a failed code file keeps its last success: its row is `applying` now, and the next deployment runs it with any content), line 342 (In-Memory OLTP: "change the file or delete its row to run it again" goes, the file runs again); §15: the table (line 1017, statuses `applied`, `applying`, `removed`), line 1020, the comment of the query (line 1037)
- `docs/deployment/code.md` (lines 93 and 101), `docs/deployment/index.md` (line 191)
- The release note of 0096-141: "A failed code file or breaking script of the Code folder writes no row: its row keeps its last success" is wrong for a code file now; the part on the breaking scripts of the Code folder stays.

## Done when
- [ ] Before the code files run, those with an `applied` row are marked `applying` with one command; a successful run writes `applied`, a failed one leaves `applying`; unit tests
- [ ] Both providers: a code file that failed runs again when its previous content comes back, with tests
- [ ] SQL Server: an In-Memory OLTP file that failed after an earlier batch committed is restored by its previous content, with a test
- [ ] `DbLiveTablesSnapshotTests` updated; `MinCompatibleVersion` stays 2.0.0
- [ ] The release note of 0096-141 corrected
- [ ] Docs updated
- [ ] Closed before the 2.0.0 release

## Notes
- 2026-11-23, the maintainer: mark a code file `applying` before it runs, so that a file whose run is not confirmed runs again on the next deployment. This revises the decision of 0096-141 not to withdraw the last success: the mark is written before the run, not in `catch`, and it also covers a lost `applied` row (case 2).
- 2026-11-23, the maintainer: no such mark for migrations. It would change nothing for the next deployment (Problem), only the log: a migration started by an earlier deployment, or a migration with `None` whose content changed after a failed run.
- The files of all groups are marked before the first group runs. A file of a later group that never started (an earlier group failed) stays `applying`, and after its previous content comes back it runs once more: harmless for code.
