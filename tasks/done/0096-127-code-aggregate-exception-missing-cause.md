---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The docs of `CodeDeploymentAggregateException` miss a code file that ended its transaction

## Release note
None. A correction of `docs/llms-full.txt` and of the XML doc of `CodeDeploymentAggregateException`; DbLive behaves as before.

## Problem
The row `CodeDeploymentAggregateException` of §17 (`docs/llms-full.txt:1069`) and its XML doc (`src/DbLive/Deployers/Code/CodeDeploymentAggregateException.cs:4-5`) list two causes: a round that deployed no file of its group, and a file whose error ended the deployment transaction.

A third one: a code file that ran `COMMIT` or `ROLLBACK` is not retried with any `TransactionWrapLevel` (`CodeItemDeployer.cs:83-91`: "The file ended its transaction, so it is not retried."), and `CodeDeployer` throws `CodeDeploymentAggregateException` with that one error (`CodeDeployer.cs:170-176`, `:217-223`).

The row "The script ended the transaction that DbLive runs it in" of §17 (`:1091`) and §10 (`:718`) say that the error is "inside `MigrationDeploymentException`, `CodeDeploymentException`, `FolderDeploymentException`". For a code file the caller gets `CodeDeploymentAggregateException`, with the `CodeDeploymentException` inside.

## Fix
Add the cause to the row and the XML doc. In `:1091` and `:718`, say that the `CodeDeploymentException` of a code file is inside `CodeDeploymentAggregateException`.

## Docs to update
- `docs/llms-full.txt` §10, §17
- the XML doc of `CodeDeploymentAggregateException`

## Done when
- [x] Docs updated

## Notes
- The `CodeDeploymentException` of a breaking script of the Code folder that ended its transaction (with `TransactionWrapLevel: Deployment`) is not inside `CodeDeploymentAggregateException`: `CodeBreakingDeployer` throws it itself (`CodeBreakingDeployer.cs:61`). So `:718` and `:1091` keep `CodeDeploymentException` in the list and say that the one of a code file is inside `CodeDeploymentAggregateException`.
- `docs/deployment/code.md:78` already says that such a code file is not retried and names no exception; not changed.

## Result
- `docs/llms-full.txt` §17, the row `CodeDeploymentAggregateException`: the third cause, a code file that ended the transaction it ran in with `COMMIT` or `ROLLBACK`, with every `TransactionWrapLevel`; it is not retried, and the deployment fails at once with its error inside. The fix column says to remove `COMMIT` and `ROLLBACK` from a code file.
- §10 (`:718`) and the row "The script ended the transaction that DbLive runs it in" of §17: the `CodeDeploymentException` of a code file is inside `CodeDeploymentAggregateException`.
- The XML doc of `CodeDeploymentAggregateException` lists the third cause. `src/DbLive` builds without warnings.
- No behavior change, no tests.
