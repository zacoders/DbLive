---
type: bug
priority: medium
created: 2026-11-14
---
# `DeploymentLoginPermissionsTests` drops the login while its killed sessions still exist

## Problem
`DropLoginAsync` kills the sessions of the test login and drops the login in one batch. A killed session can still be in `sys.dm_exec_sessions` when `drop login` runs, and the test fails with "Could not drop login 'dblive-deploy-<guid>' as the user is currently logged in." Found while working on 0096-81, 2026-11-14 (verified): in full runs of `DbLive.MSSQL.Tests` one of the two tests of the class failed so in 1 of 3 runs on 435e5f2, before the changes of 0096-81, and in 2 of 3 runs with them; the class alone passes. CI can fail on it with no change in the code.

## Where
`src/DbLive.MSSQL.Tests/DeploymentLoginPermissionsTests.cs`, `DropLoginAsync`.

## Fix
Wait until the sessions of the login are gone before `drop login`: a loop with `waitfor delay '00:00:00.100'` while `sys.dm_exec_sessions` has a row of the login, with a limit; or retry `drop login` on its error 15434.

## Done when
- [ ] `DropLoginAsync` waits for the killed sessions; repeated full runs of `DbLive.MSSQL.Tests` pass

## Notes
- 2026-11-15, while working on 0096-76 (verified): both tests of the class failed in a run of the whole solution, one of them in the next run of `DbLive.MSSQL.Tests`; the class alone passed. These tests run no SQL test, so 0096-76 does not touch them.
