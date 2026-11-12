---
type: bug
priority: medium
created: 2026-11-12
---
# A code file uses up its retries while the file it depends on is still being deployed

## Problem
Code files of a group are deployed by several workers from one queue. A file that fails goes to the end of the queue and is tried again, `MaxCodeDeployRetries` (5) times in all. The docs say that this resolves the order of dependencies (§6: "a view that uses a function deployed later succeeds on retry").

A failed file is put back at once, and any free worker takes it at once. Nothing waits for the files that other workers are deploying at that moment. So when file B needs file A, and A is still running on another worker, B fails five times within a few milliseconds and the deployment fails, although A completes a moment later.

Verified on SQL Server 2022 (a temporary harness, not kept), default settings, two code files:

```sql
-- Code/a_base.sql
waitfor delay '00:00:03';
go
create or alter view dbo.v_base as select Id from dbo.T;

-- Code/b_dependent.sql
create or alter view dbo.v_dependent as select Id from dbo.v_base;
```

The deployment fails after 3.9 seconds with `CodeDeploymentAggregateException` (`Invalid object name 'dbo.v_base'`). `dblive.code` has `a_base.sql` as `applied` and `b_dependent.sql` as `error`. The next deployment succeeds.

The delay stands for anything that makes A slower than five failed attempts of B: A waits for a schema lock on a busy database (an `alter` of an object in use), builds an indexed view, or is only a large file. Then the deployment fails now and then, and passes when it is run again.

Since 0096-44 only the files that changed enter the queue. Before, the unchanged files were in the queue too and stood between two attempts of a file. Now a release that changes two files, one of which uses a new column of the other, has a queue of these two files.

Found by the code review of 2026-11-12 (finding 4).

## Where
- `src/DbLive/Deployers/Code/CodeDeployer.cs:152-217` (`CreateWorkerAsync`: `queue.Enqueue(codeItem)` at 214, taken again by any worker; the retry counter at 178-204)
- `src/DbLive/Deployers/Code/CodeDeployer.cs:125-136` (the workers)

## Fix
Retry in rounds instead of at once. The workers deploy all files of the round; the files that failed form the next round, which starts when every worker has finished the round before. A file is then never tried again while a file of the same round is still running.

The deployment fails when a round deploys no file (no progress), or after `MaxCodeDeployRetries` rounds. A file whose error ended the deployment transaction still stops the deployment at once (0096-61).

- [ ] Keep `MaxCodeDeployRetries` as the limit of rounds, or drop the limit and stop only when a round makes no progress? A chain of six files in the wrong order needs six rounds today and fails with 5.

## Docs to update
- `docs/llms-full.txt` §6 (execution order and retries), §9.2 (`MaxCodeDeployRetries`), §17 (`CodeDeploymentAggregateException`)
- `docs/deployment/code.md` ("Order, parallelism and retries")

## Done when
- [ ] A file that fails while its dependency is being deployed by another worker is deployed in a later round; unit test with a slow dependency
- [ ] The retries in the deployment transaction (one worker) work as before
- [ ] Docs updated
