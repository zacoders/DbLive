---
type: bug
priority: low
created: 2026-11-17
---
# A failed write of the error row replaces the error of a Code breaking script

## Problem
When a `*.breaking.sql` of the Code folder fails outside a transaction, `CodeBreakingDeployer` writes its error row in the `catch` block, and that write is not protected. When the write fails too (the failing script broke the connection, the server is unreachable, the login has no `INSERT` permission), its exception leaves the `catch`: the caller gets a raw exception of the provider instead of `CodeDeploymentException` with the path of the file and the SQL error.

`CodeItemDeployer.SaveErrorRowAsync` protects the same write for code files since 0096-68: a failed write is logged as a warning, and the error of the file stays the result. From the code, not reproduced.

The comment on line 43 ("one command without its own transaction, like a code file") is stale since 0096-83: a code file now runs in a transaction of its own.

Found by the code review of 2026-11-17 (finding 3).

## Where
- `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs:56-59` (the write), `:43` (the comment)
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:90-114` (`SaveErrorRowAsync`, the protected version)

## Fix
Wrap the write of the error row in `try`/`catch` and log its exception as a warning, as `SaveErrorRowAsync` does, so that `CodeDeploymentException` is always the one thrown. Correct the comment: the script runs without a transaction of its own, unlike a code file.

## Done when
- [ ] Fixed, with a unit test in `CodeBreakingDeployerTests`: `SaveCodeItemAsync` throws, and the deployment fails with `CodeDeploymentException` that names the file and holds the error of the script
- [ ] The comment on line 43 corrected
