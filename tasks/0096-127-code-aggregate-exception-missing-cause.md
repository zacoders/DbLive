---
type: docs
priority: low
created: 2026-11-23
---
# The docs of `CodeDeploymentAggregateException` miss a code file that ended its transaction

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
- [ ] Docs updated
