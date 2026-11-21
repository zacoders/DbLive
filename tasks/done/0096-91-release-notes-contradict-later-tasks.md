---
type: docs
priority: high
created: 2026-11-17
closed: 2026-11-22
---
# Release notes of 2.0 contradict later tasks of the same release

## Release note
None. The release notes of 0096-17, 0096-35, 0096-40, 0096-45, 0096-46, 0096-59, 0096-63, 0096-68, 0096-76, 0096-83, 0096-84 and 0096-86 now describe the change from 1.5.0 to 2.0.

## Problem
The GitHub Release of 2.0.0 is built from the `## Release note` sections of the tasks closed since 1.5.0. Some notes describe a state that a later task of the same release changed, so the release contradicts itself (seen in the output of `dotnet run .github/scripts/release-notes.cs -- --check`, 172 lines):

- 0096-68: "A file that fails on every attempt fails the deployment after `MaxCodeDeployRetries` attempts". 0096-77 removed `MaxCodeDeployRetries`: a group stops when a round deploys no file.
- 0096-83: "`CodeItemTimeout` stays the timeout of each batch". 0096-84 made it the timeout of the whole file.
- 0096-35, 0096-40 and 0096-63 name the namespaces their exceptions had then: `DatabaseNotFoundException` in `DbLive.Adapter`, `DatabaseRequiresNewerDbLiveException` in `DbLive.SelfDeployer`, `FolderDeploymentException` in `DbLive.Deployers.Folder`. 0096-73 moved all three to `DbLive` (`PublicAPI.Shipped.txt`), and its own note says "The exceptions are in `DbLive` now". A `using` or a `catch` written from the older notes does not compile (CS0234). Found by the second code review of 2026-11-17 (finding 4); the other notes that name a namespace (0096-30-02, 0096-31-01) are right.

Only these terms were searched for; the generated notes were not read from start to end. 0096-66 fixed the same kind of problem for undo scripts.

Found by the code review of 2026-11-17 (finding 5).

## Where
- `tasks/done/0096-68-transaction-check-hides-code-file-error.md:10`
- `tasks/done/0096-83-code-file-partly-applied-on-sql-server.md:11`
- `tasks/done/0096-35-missing-database-error.md:10`, `tasks/done/0096-40-older-dblive-on-newer-database.md:10`, `tasks/done/0096-63-folder-script-error-without-file-name.md:11`

## Fix
Correct these notes so that they describe the change from 1.5.0 to 2.0. Then read the whole preview once and correct every other statement that a later task changed.

0096-88 and 0096-90 may change the notes of 0096-83 and 0096-77. Do the full read after them, or repeat it.

## Done when
- [x] The notes of 0096-35, 0096-40, 0096-63, 0096-68 and 0096-83 corrected
- [x] The whole output of `dotnet run .github/scripts/release-notes.cs` read; other contradictions corrected and listed under `## Notes`
- [x] `dotnet run .github/scripts/release-notes.cs -- --check` passes

## Notes
- The full read was done on 2026-11-22, after 0096-88 and 0096-90 were closed (181 lines). 0096-88 had already corrected the note of 0096-83 on In-Memory OLTP, and 0096-90 the notes of 0096-46 and 0096-77 on `InvalidProjectSettingsException`.
- Other contradictions found by the full read and corrected (each checked against the code or `PublicAPI.Shipped.txt`):
  - 0096-17: "`DbLiveVisualStudioTester` is renamed to `DbLiveTester`, and `IUnitTestsRunner.RunAllTestsAsync()` takes no arguments". 0096-73 made both internal; the public interface is `IDbLiveTester` from `CreateTester()` (1.5.0 had `DbLiveVisualStudioTester` and `IUnitTestsRunner` public).
  - 0096-45: "the fixture is now `DeploymentTest`". 0096-81: `DeploymentTest` is not an xUnit fixture. Now "`DeployFixture` is now `DeploymentTest`".
  - 0096-46: the bullet "API: `MigrationItemType.Undo`, `MigrationItemStatus.Reverted`, `IDbLiveDA...` are removed; `IMigrationVersionDeployer.DeployAsync` takes only the migration" removed. 0096-73, 0096-80 and 0096-81 made all these types internal (none is in `PublicAPI.Shipped.txt`), and their notes say so.
  - 0096-59: "The message of `DbLiveSqlException` on PostgreSQL now holds ... `Timeout during reading attempt` for a timeout". 0096-84 made a timeout `ExecutionTimeoutException` with its own message and the error of Npgsql inside. The note now speaks of an error of Npgsql (a lost connection keeps the chain, `PostgreSqlDA`) and names `ExecutionTimeoutException` for a timeout.
  - 0096-68: "or outlived `DeploymentTimeout` ... now fails with `CodeDeploymentAggregateException`". 0096-86: with `DeploymentTimeout` the outer exception is `ExecutionTimeoutException`, with `CodeDeploymentAggregateException` inside. Added.
  - 0096-76: "PostgreSQL is not affected" and "What was committed still stays in the database". 0096-87: `commit` or `rollback` in a PostgreSQL test fails it too, and DbLive rolls back what ran after the end of the transaction (`UnitTestItemRunner.TransactionEndedMessage`, `MsSqlDA` with `IMPLICIT_TRANSACTIONS`).
  - 0096-84: "a code file committed its later batches one by one while the transaction of the file was lost". The transaction of a code file came with 0096-83 of the same release; in 1.5.0 a code file had no transaction, and SMO ran its batch twice like any other script without one (the table "The retry of SMO after a lost connection" in 0096-84).
  - 0096-86: "Before, ... on PostgreSQL `ExecutionTimeoutException` that named the timeout of the script". That came with 0096-84 of the same release; 1.5.0 gave the error of Npgsql, `Exception while reading from stream`.
- Read and left as they are: 0096-80 names members of `IDbLiveDA` as internal and 0096-81 the whole interface, both true in 2.0. 0096-44 (files read once per builder) and 0096-64 (a failed read is repeated) agree, 0096-64 says so. 0096-60 and 0096-85 on the `error` column add to each other.
- `--check` without `--title` reads the title of the merged PR from `HEAD` and fails on this branch (patch instead of major, `MinCompatibleVersion`); with `--title "BREAKING CHANGE: Documentation"`, as the PR of #96 will have, it passes and gives 2.0.0.
- Third time the same kind of problem (0096-66, the correction of 0096-83 by 0096-88, this task): a rule added to the close step in `AGENTS.md` and `tasks/README.md`. A task corrects the notes of earlier tasks of the release that it makes wrong, in the same commit. Decision of the maintainer (2026-11-22): fix the notes and add the rule.

## Result
- Corrected the release notes of 0096-17, 0096-35, 0096-40, 0096-45, 0096-46, 0096-59, 0096-63, 0096-68, 0096-76, 0096-83, 0096-84 and 0096-86 (see Notes).
- `AGENTS.md` (Close, new step 2) and `tasks/README.md` (the paragraph on release notes and the Close step of the workflow): a task that makes the release note of an earlier task of the same release wrong corrects it in the same commit.
- `dotnet run .github/scripts/release-notes.cs -- --check --title "BREAKING CHANGE: Documentation"` passes (2.0.0); none of the corrected statements is in its output.
