---
type: chore
priority: low
created: 2026-11-26
closed: 2026-11-26
---
# A test of the deployment timeout fails under load

## Release note
None. A test of this repository; DbLive behaves as before.

## Problem
`CodeRetryInDeploymentTransactionTests.Code_file_that_outlives_the_deployment_timeout_fails_the_deployment_with_its_path` (`src/DbLive.MSSQL.Tests/CodeRetryInDeploymentTransactionTests.cs:112`) runs a code file of `waitfor delay '00:00:04'` with `DeploymentTimeout` 2 s and expects `ExecutionTimeoutException`. In `dotnet test src/DbLive.sln`, with all test projects running, the deployment ran the file to its end and committed: `Assert.Throws() Failure: No exception was thrown` (verified once, 2026-11-26, while working on 0096-189). Alone, the class passed 3 of 3 runs, and the whole SQL Server project passed right after.

The same cause as in 0096-86 (Notes, "The timer of System.Transactions under load"): the timer of the transaction timeout fires late under load. There `TransactionTimeoutTests` of both providers went into a collection without parallelization, and the SQL Server deployment runs 10 s, not 5. This test was not moved.

## Fix
- [x] Move the test into the collection of `TransactionTimeoutTests` (`[Collection(nameof(TransactionTimeoutTests))]`), and give the file more time over the timeout, as there? Yes: the test is now in `TransactionTimeoutTests` (`src/DbLive.MSSQL.Tests/TransactionTimeoutTests.cs`), and its file runs 10 s against `DeploymentTimeout` 2 s.
- [x] Does the PostgreSQL project have a test of the same kind outside that collection? No: only `TransactionTimeoutTests` set `DeploymentTimeout`; the waits in `CommandTimeoutTests` of both providers end by the command timeout, not by the timer of System.Transactions.

## Done when
- [x] The test does not depend on the load of the machine

## Notes
- Found while working on 0096-189.

## Result
The test moved from `CodeRetryInDeploymentTransactionTests` to `TransactionTimeoutTests`, whose collection does not run in parallel with the other tests of the project; `CodeRetryInDeploymentTransactionTests.CreateProject` is `internal` for it. The code file waits 10 s instead of 4, so the timer has 8 s to fire; the file is not interrupted, so the test takes 10 s. Both classes pass (9 tests).
