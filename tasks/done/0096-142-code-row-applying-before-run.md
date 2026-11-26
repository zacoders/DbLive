---
type: bug
priority: high
created: 2026-11-23
closed: 2026-11-23
---
# A code file whose run is not confirmed is skipped when its previous content comes back

## Release note
**A code file whose run is not confirmed runs again on the next deployment, whatever its content.** Before the code files run, the rows in `dblive.code` of the changed files get the new status `applying`, with one command; a successful run sets `applied`. A file that failed keeps `applying` and runs on the next deployment also when its content comes back to the one of its last success (`git revert`, a redeploy of the previous release), because the failed run may have changed the database; DbLive 1.5 did the same with the status `error`. New is a file that committed when its row was not written (a broken connection, a failover, a stopped release job): it runs again too, while before it was skipped.

To upgrade: nothing to change. A query or report that reads `dblive.code.status` gets the value `applying` for a code file whose last run is not confirmed; its `content_hash` and `applied_utc` are of its last success.

## Problem
The row of a code file in `dblive.code` keeps the hash of its last success while the file runs and after it fails (0096-141). The next deployment skips the file as `[hash match]` when its content returns to that hash (`git revert`, a redeploy of the previous release). That is wrong when the run that was not confirmed changed the database:

1. SQL Server, a code file with In-Memory OLTP DDL (`RunsCodeItemWithoutTransaction`): its `GO` batches commit one by one. A later batch fails after an earlier one committed (the `drop` succeeds, the `create` fails), or the process stops between them: the object is missing or changed in part, and the previous content, which would restore it, is skipped. Today §6 tells the user to change the file or delete its row.
2. Both providers, every code file outside the deployment transaction: the file commits, and its `applied` row is not written: the write fails (the connection breaks, a failover), or the process stops before it (a release job cancelled or timed out, the agent stopped). `CodeItemDeployer` runs the file and writes its row with two commands (`ExecuteCodeItemAsync`, `SaveCodeItemAsync`), not in one transaction. The object is the new version, the row is the old one; when the previous content comes back, the file is skipped and the object stays the new version. It goes unnoticed: the object works, only not as the project says.

The second condition is the usual reaction to the first: a failed release is rolled back to the previous one. DbLive 1.5 wrote a row with `status = 'error'` and the failed hash, which ran the file again in case 1; 0096-141 dropped it. Case 2 was not covered by 1.5 either: the write of the error row goes to the same server right after the failure, and a stopped process writes nothing.

0096-141 decided not to withdraw the last success on failure (its Notes): a write in `catch` is what 0096-51, 0096-60 and 0096-89 were about, and it cannot help in case 2. A mark written before the run covers both cases, also a stopped process, and writes nothing on failure.

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
7. The previous content restores the object only when it runs from the state the failed run left. A code file runs on every change of its content anyway, so it is written to run again (`create or alter`, `drop ... if exists`); §6 says it for In-Memory OLTP files, whose batches can leave such a state.
8. Compatibility: `applying` is a new stored value ("Compatibility of the `dblive` tables" in `Development.md`). 2.0 is not released and `MinCompatibleVersion` is 2.0.0 already: it stays when this task is in 2.0. After the release it would raise the minimum and block a rollback to 2.0, the case this task is about. No self-migration: `status` is `varchar(32)` without a check on both providers.

## Docs to update
Line numbers as of 2026-11-23.
- `docs/llms-full.txt` §6: line 337 (a failed code file keeps its last success: its row is `applying` now, and the next deployment runs it with any content), line 342 (In-Memory OLTP: "change the file or delete its row to run it again" goes, the file runs again); §15: the table (line 1017, statuses `applied`, `applying`, `removed`), line 1020, the comment of the query (line 1037)
- `docs/deployment/code.md` (lines 93 and 101), `docs/deployment/index.md` (line 191)
- The release note of 0096-141: "A failed code file or breaking script of the Code folder writes no row: its row keeps its last success" is wrong for a code file now; the part on the breaking scripts of the Code folder stays.
- The release note of this task: a query or report that reads `dblive.code.status` gets the new value `applying`.

## Done when
- [x] Before the code files run, those with an `applied` row are marked `applying` with one command; a successful run writes `applied`, a failed one leaves `applying`; unit tests
- [x] Both providers: a code file that failed runs again when its previous content comes back, with tests
- [x] SQL Server: an In-Memory OLTP file that failed after an earlier batch committed is restored by its previous content, with a test
- [x] `DbLiveTablesSnapshotTests` updated; `MinCompatibleVersion` stays 2.0.0
- [x] The release note of 0096-141 corrected
- [x] Docs updated
- [x] Closed before the 2.0.0 release

## Notes
- 2026-11-23, the maintainer: mark a code file `applying` before it runs, so that a file whose run is not confirmed runs again on the next deployment. This revises the decision of 0096-141 not to withdraw the last success: the mark is written before the run, not in `catch`, and it also covers a lost `applied` row (case 2).
- 2026-11-23, the maintainer: no such mark for migrations. It would change nothing for the next deployment (Problem), only the log: a migration started by an earlier deployment, or a migration with `None` whose content changed after a failed run.
- The files of all groups are marked before the first group runs. A file of a later group that never started (an earlier group failed) stays `applying`, and after its previous content comes back it runs once more: harmless for code.
- 2026-11-23, the maintainer: the Fix as written (the `applying` mark), after a review of the alternatives:
  - The `applied` row in the same transaction as the file (the provider writes it before its `commit`, on the connection of the file): closes case 2 for a file in a transaction of its own, not case 1, which has no transaction, and needs a change of `IDbLiveDA.ExecuteCodeItemAsync`. With the mark it adds nothing.
  - A hash that never matches instead of a new status (`content_hash = 0` before the run): an older DbLive reads it as "not deployed", so it would not raise `MinCompatibleVersion` even after the 2.0 release; but the row loses the hash of its last success, and the value is a magic one. A way out only if this task had missed 2.0.
  - The mark for In-Memory OLTP files only (`RunsCodeItemWithoutTransaction`): case 1 only.
  - The state of 0096-141 with docs: case 2 stays silent and undocumented.
- Case 2 reproduced on both providers with a trigger on `dblive.code` that fails the update to `applied`: the file commits, its row write fails, as with a broken connection. Verified: with the mark disabled, the three new integration tests of SQL Server fail (the previous content is skipped as `[hash match]`); with it, they pass.
- The mark sets `status = 'applying'` only where `status = 'applied'` (`MarkCodeAsApplyingAsync`), so a `removed` row stays `removed` whatever the list says. On SQL Server with an accent-insensitive collation two paths that differ only by accent share one row (the key of `dblive.code`); marking it changes nothing for them: such files already run on every deployment.
- Unrelated: `SqlTestResultSetTests.Rows_test_compares_NaN_and_infinity` (PostgreSQL) fails in a Windows checkout with `core.autocrlf = true`: the expected text is a raw literal, CRLF in such a checkout, and the message is compared after `ReplaceLineEndings("\n")`. It passes with LF sources (CI). Fixed in 0096-143.

## Result
- **Code.** `CodeItemStatus.Applying`. `IDbLiveDA.MarkCodeAsApplyingAsync`, on SQL Server through the xml list of paths and `#paths` like `MarkCodeAsVerifiedAsync`, on PostgreSQL with a `text[]` parameter; it changes only `applied` rows. `CodeDeployer.SkipDeployedAsync` collects the files to run that have an `applied` row (`CodeRows.HasAppliedRow`) in all groups and marks them with one command before the first file runs; nothing is sent when there are none (a first deployment). `CodeItemDeployer` is unchanged: a success writes `applied`, a failure writes nothing. `CodeRows.IsDeployed`, the guard of the breaking phase and `MarkMissingCodeAsRemovedAsync` are unchanged.
- **Compatibility.** `applying` is a new stored value: `DbLiveTablesSnapshotTests` updated, `MinCompatibleVersion` stays 2.0.0 (2.0 is not released). No self-migration.
- **Tests.** `CodeDeployerTests`: the files to run with an `applied` row are marked, not those without a row, with a `removed` row or already `applying`; the files of all groups are marked before the first file runs; an error of the mark fails the deployment before any file runs; a first deployment sends no mark. `BreakingChangesDeployerTests`: an `applying` row with the current hash is not deployed for the guard. `CodeFileFailureTests` on both providers: a failed file leaves `applying` with the hash and time of its last success, runs again, and runs when the content of its last success comes back; a file that committed when its row write failed (a trigger) is restored by its previous content. `CodeFileTransactionTests` (SQL Server): an In-Memory OLTP file whose `drop` committed and `create` failed is restored by its previous content. Verified: `DbLive.Tests` 551, `DbLive.MSSQL.Tests` 155, all passed; `DbLive.PostgreSQL.Tests` 86 of 87, the one failure unrelated (Notes).
- **Docs.** `docs/llms-full.txt` §6 (the mark, a failed file, In-Memory OLTP files) and §15 (the statuses, the failures, the query); `docs/deployment/code.md`, `docs/deployment/index.md`.
- **Other tasks.** The release note of 0096-141: the sentence on a failed code file's row removed; the one on the breaking scripts of the Code folder stays.
