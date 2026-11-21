---
type: bug
priority: low
created: 2026-11-17
closed: 2026-11-21
---
# A failed write of the error row replaces the error of a Code breaking script

## Release note
**A failed breaking script of the Code folder keeps its error when its error row cannot be saved.** When a `*.breaking.sql` of the Code folder failed outside a transaction and the write of its row with the error to `dblive.code` failed too (the server became unreachable, for one), the deployment failed with the error of that write instead of `CodeDeploymentException` with the path of the script and its SQL error. It now fails with `CodeDeploymentException`, and the failed write is logged as a warning, as for code files.

Nothing to change on upgrade.

## Problem
When a `*.breaking.sql` of the Code folder fails outside a transaction, `CodeBreakingDeployer` writes its error row in the `catch` block, and that write is not protected. When the write fails too, its exception leaves the `catch`: the caller gets a raw exception of the provider instead of `CodeDeploymentException` with the path of the file and the SQL error. The docs promise `CodeDeploymentException` (`docs/llms-full.txt` §6.1).

`CodeItemDeployer.SaveErrorRowAsync` protects the same write for code files since 0096-68: a failed write is logged as a warning, and the error of the file stays the result. From the code, not reproduced.

The comment on line 43 ("one command without its own transaction, like a code file") is stale since 0096-83: a code file now runs in a transaction of its own.

Found by the code review of 2026-11-17 (finding 3).

## Where
- `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs:56-59` (the write), `:43` (the comment)
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:90-114` (`SaveErrorRowAsync`, the protected version)

## Fix
Wrap the write of the error row in `try`/`catch` and log its exception as a warning, as `SaveErrorRowAsync` does, so that `CodeDeploymentException` is always the one thrown. Correct the comment: the script runs without a transaction of its own, unlike a code file.

## Done when
- [x] Fixed, with a unit test in `CodeBreakingDeployerTests`: `SaveCodeItemAsync` throws, and the deployment fails with `CodeDeploymentException` that names the file and holds the error of the script
- [x] The comment on line 43 corrected

## Notes
- Of the causes the review named, only "the server is unreachable" is real:
  - A script that broke its connection does not make the write fail: each call of the data access opens its own pooled connection (`MsSqlDA.SaveCodeItemAsync`), and 0096-68 verified that the write of the error row succeeds after `raiserror(..., 20, 1) with log` in a code file without a transaction.
  - A login without `INSERT` on `dblive.code` is an operator error, and the deployment fails earlier: the self-deployment and the rows of the code files write there too.
- The error of the script was not lost before the fix either: `CodeBreakingDeployer` logs it as an error with the path before the write. Only the exception thrown was wrong.
- `CodeBreakingDeployerTests.Failed_script_fails_with_the_error_of_its_error_row_when_the_row_cannot_be_saved` (added by the code review of 2026-11-07) asserted the old behavior; it is replaced, not kept beside the new test.
- No docs to update: `docs/llms-full.txt` §6.1 and `docs/deployment/breaking-changes.md` already say that a failed script stops the deployment with `CodeDeploymentException`.

## Result
- `CodeBreakingDeployer` writes the error row through `SaveErrorRowAsync`: a failed write is logged as a warning ("The error row of {FilePath} is not saved."), and `CodeDeploymentException` with the path and the error of the script is thrown. The same shape and message as `CodeItemDeployer.SaveErrorRowAsync`; no shared helper for two places whose rows differ.
- The comment on the execution of the script says it runs without a transaction of its own, unlike a code file.
- Tests: `CodeBreakingDeployerTests.Failed_script_keeps_its_error_when_the_error_row_cannot_be_saved` replaces the test of the old behavior. All 542 tests of `DbLive.Tests` pass.
