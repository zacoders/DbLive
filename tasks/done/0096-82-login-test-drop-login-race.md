---
type: bug
priority: medium
created: 2026-11-14
closed: 2026-11-17
---
# `DeploymentLoginPermissionsTests` drops the login while its killed sessions still exist

## Release note
None. Tests only.

## Problem
`DropLoginAsync` kills the sessions of the test login and drops the login in one batch. A killed session can still be in `sys.dm_exec_sessions` when `drop login` runs, and the test fails with "Could not drop login 'dblive-deploy-<guid>' as the user is currently logged in." Found while working on 0096-81, 2026-11-14 (verified): in full runs of `DbLive.MSSQL.Tests` one of the two tests of the class failed so in 1 of 3 runs on 435e5f2, before the changes of 0096-81, and in 2 of 3 runs with them; the class alone passes. CI can fail on it with no change in the code.

## Where
`src/DbLive.MSSQL.Tests/DeploymentLoginPermissionsTests.cs`, `DropLoginAsync`.

## Fix
Wait until the sessions of the login are gone before `drop login`: a loop with `waitfor delay '00:00:00.100'` while `sys.dm_exec_sessions` has a row of the login, with a limit; or retry `drop login` on its error 15434.

## Done when
- [x] `DropLoginAsync` waits for the killed sessions; repeated full runs of `DbLive.MSSQL.Tests` pass

## Notes
- 2026-11-15, while working on 0096-76 (verified): both tests of the class failed in a run of the whole solution, one of them in the next run of `DbLive.MSSQL.Tests`; the class alone passed. These tests run no SQL test, so 0096-76 does not touch them.
- 2026-11-17 (verified): with a temporary counter of the waits, 6 full runs of `DbLive.MSSQL.Tests` dropped the login 12 times; in 3 of them a session of the login was still in `sys.dm_exec_sessions` right after `kill`, and one wait of 100 ms was enough. Each of the 12 drops found 2 sessions of the login before `kill`. Without the wait those 3 drops would have failed with error 15434.

## Result
`DropLoginAsync` kills the sessions of the login, then waits in 100 ms steps while `sys.dm_exec_sessions` still has a session of the login, for at most 10 seconds, and then drops the login. After 10 seconds `drop login` runs anyway and fails with the error of the server, so a session that does not end is still reported. The wait is in the same T-SQL batch, so no retry in C# was needed.

Tests: 6 runs of `DbLive.MSSQL.Tests` and then 5 runs of the whole solution (746 tests each) passed.
