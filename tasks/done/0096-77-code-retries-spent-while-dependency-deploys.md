---
type: bug
priority: medium
created: 2026-11-12
closed: 2026-11-16
breaking: true
---
# A code file uses up its retries while the file it depends on is still being deployed

## Release note
**Code files are retried in rounds, and `MaxCodeDeployRetries` is removed.** A code file that fails is retried in the next round, after every file of its group has finished. Before, it went back to the queue at once: a file that needed a slow file running on another worker (one that waits for a schema lock, builds an indexed view, or is large) used up its five attempts within milliseconds, and the deployment failed now and then. A group now stops only when a round deploys no file, so dependencies inside a group deploy in any order, also a chain longer than five files. A file that fails for another reason than a missing dependency is no longer tried five times: the deployment fails with its error in the first round where no file succeeds.

To upgrade: remove `MaxCodeDeployRetries` from `settings.json`. A `settings.json` that still has it fails with `InvalidProjectSettingsException`.

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
- `src/DbLive/Deployers/Code/CodeDeployer.cs:149-214` (`CreateWorkerAsync`: `queue.Enqueue(codeItem)` at 211, taken again by any worker; the retry counter at 175-201)
- `src/DbLive/Deployers/Code/CodeDeployer.cs:98-147` (`DeployGroupAsync`: the workers at 126-133)
- `src/DbLive/Common/Settings/DbLiveSettings.cs:40` (`MaxCodeDeployRetries`)

## Fix
Retry in rounds instead of at once. The workers deploy all files of the round; the files that failed form the next round, which starts when every worker has finished the round before. A file is then never tried again while a file of the same round is still running.

The deployment fails when a round deploys no file (no progress), with the errors of the files of that round. `MaxCodeDeployRetries` is removed. A file whose error ended the deployment transaction still stops the deployment at once (0096-61).

- [x] Keep `MaxCodeDeployRetries` as the limit of rounds, or drop the limit and stop only when a round makes no progress? A chain of six files in the wrong order needs six rounds today and fails with 5. Drop the setting (decision of 2026-11-16, see Notes).

## Docs to update
- `docs/llms-full.txt` §6 (execution order and retries), §9.2 (`MaxCodeDeployRetries`), §17 (`CodeDeploymentAggregateException`)
- `docs/deployment/code.md` ("Order, parallelism and retries")
- `docs/project/configuration.md` ("Parallelism": `MaxCodeDeployRetries`)

## Done when
- [x] A file that fails while its dependency is being deployed by another worker is deployed in a later round; unit test with a slow dependency
- [x] A chain of dependencies longer than five files in the wrong order deploys; a group stops when a round deploys no file
- [x] The retries in the deployment transaction (one worker) work as before
- [x] `MaxCodeDeployRetries` removed
- [x] Docs updated

## Notes
- **Groups and retries.** Code groups are deployed one after another: `DeployAsync` awaits `DeployGroupAsync` for each group (`CodeDeployer.cs:25-28`). A group is one entry of `CodeSubFoldersDeploymentOrder`, and the rest of the code is the last group (`DbLiveProject.cs:57-83`). The queue and the retries belong to one group, so retries resolve dependencies inside a group only: a file that needs a file of a later group fails. This bug is inside one group, and with the default `CodeSubFoldersDeploymentOrder: []` all code is one group.
- **Why there are groups.** They came in `1312c1b` (#13, 2024-02-05). Then each group ran once with `Parallel.ForEach`, and any error failed the deployment: a group listed first was the only way to order dependencies (the demo moved its schemas to `Code/schemas` in that commit). The queue with retries inside a group came in `240c11a` (#54, 2025-12-27), so that users need not list their dependencies. A round does inside a group what groups do between them: it starts after the round before has finished. Groups stay for what retries do not fix: schemas and types that everything needs, and the errors that end the deployment transaction (0096-61).
- **Workaround before the fix:** put the slow dependency into a group listed earlier in `CodeSubFoldersDeploymentOrder`.
- **Decision of the maintainer (2026-11-16):** remove `MaxCodeDeployRetries` and retry in rounds until a round deploys no file. Fewer settings, and a long chain of dependencies no longer fails the deployment. The number of rounds is bounded without the setting: every round but the last deploys at least one file, so a group of N files takes at most N rounds.
- `settings.json` is read with `UnmappedMemberHandling.Disallow`, so a file that still has `MaxCodeDeployRetries` fails with `JsonException`, as `DowngradeTimeout` did in 0096-46: the task is `breaking: true`.
- A file that fails for another reason than a missing dependency now gets two attempts (its round, then a round where it fails alone) instead of five; a group whose files all fail in the first round is not retried. Retries are for the order of dependencies, not for transient errors: five immediate attempts sometimes hid a deadlock or a lock timeout, and now such a file is tried again only when another file of its round succeeded.
- A file with several `GO` batches that fails after its first batch created an object leaves that object; when another file of the round needed it and no file succeeded, the group stops, although the next round would deploy both. One object per file (the docs recommend it) avoids it. The maintainer accepted the case as rare (2026-11-16); since 0096-83 a code file runs in a transaction of its own, so a failed file leaves nothing on SQL Server either.
- **Verified** on SQL Server 2022 with `CodeRetryRoundsTests` (the scenario of the Problem, a delay of 1 second): the code before the fix fails with `Invalid object name 'dbo.v_base'`, the fix deploys both files. The new unit tests of the slow dependency, the chain and the stop fail on the code before the fix too.

## Result
- `CodeDeployer.DeployGroupAsync` deploys a group in rounds (`DeployRoundAsync`, `RunWorkerAsync`). The files that failed form the next round, in the order of the round. When a round deploys no file, the group fails with `CodeDeploymentAggregateException` whose inner exceptions are the errors of the files of that round, each logged with its number of attempts. A file whose error ended the deployment transaction still stops the deployment at once with its error alone (0096-61). With one worker (the deployment transaction) a round is the old pass through the queue, so the order of attempts did not change.
- `MaxCodeDeployRetries` removed from `DbLiveSettings` (decision of 2026-11-16): `breaking: true`, because `settings.json` with it no longer loads.
- Unit tests (`CodeDeployerTests`): a dependent file that fails while its dependency runs on another worker is tried again only after it (`DeployCode_Retries_A_File_After_Its_Dependency_That_Runs_On_Another_Worker`); a chain of seven files in the reverse order deploys in seven rounds (`DeployCode_Deploys_A_Chain_Of_Dependencies_In_The_Reverse_Order`); a file that always fails gets a second round when another file succeeded, and then stops the deployment (`DeployCode_Stops_When_A_Round_Deploys_No_File`); a first round without success stops at once with the errors of all its files (`DeployCode_FailedCodeItems`); the order of retries is the same in and out of the deployment transaction (`DeployCode_DependencyRetry_UsesQueueOrder`). Integration test on SQL Server: `CodeRetryRoundsTests`.
- Docs: `docs/llms-full.txt` §6, §9.2, §17; `docs/deployment/code.md` ("Order, parallelism and retries"); `docs/project/configuration.md` ("Parallelism").
