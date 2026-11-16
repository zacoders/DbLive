---
type: bug
priority: medium
created: 2026-11-16
---
# `CodeItemTimeout` limits each `GO` batch on SQL Server, not the code file

## Problem
`CodeItemTimeout` is meant as the timeout of a code file. On PostgreSQL it is: the file is one `NpgsqlCommand`, and `CommandTimeout` covers the whole command. On SQL Server SMO `ServerConnection.ExecuteNonQuery` runs each `GO` batch as a command of its own, and `StatementTimeout` applies to each of them, so a file of N batches may run almost N times `CodeItemTimeout`. The docs say so (§6: "on SQL Server it applies to each `GO` batch of the file"), but the providers differ, and the name of the setting promises a timeout of the file.

Verified on 2026-11-16 with a temporary integration test (Testcontainers, not kept), `CodeItemTimeout: 00:00:03`, one code file:

| `Code/a.sql` | SQL Server | PostgreSQL |
| --- | --- | --- |
| two batches of 2 s (`waitfor delay '00:00:02'`, `GO` between them) | deployed after 4.5 s | — |
| one batch with two `waitfor delay '00:00:02'` | fails after 3.5 s: `The wait operation timed out.` | — |
| two statements of 2 s (`select pg_sleep(2);` twice) | — | fails after 3.1 s: `Timeout during reading attempt` |
| one statement of 4 s | fails after 3.5 s | fails after 3.2 s |

Since 0096-83 the batches of a code file run in one transaction, which holds the locks of the first batches while the later ones run: a file that runs longer than its timeout now also keeps those locks longer.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs` (`ExecuteScriptAsync`: `StatementTimeout = GetTimeoutSeconds(timeout)` on the `ServerConnection`, then `ExecuteNonQuery` of the whole file)

## Fix
Decision of the maintainer (2026-11-16): `CodeItemTimeout` is the timeout of the code file, on both providers.

On SQL Server run the batches of the file one by one, and give each batch the time left of the file as its command timeout. A file that runs out of time fails with the timeout error of SQL Server and the path of the file, as a batch that runs too long does now; the transaction of the file (0096-83) is rolled back.

Not a `TransactionScope` with `CodeItemTimeout` as its timeout: when it times out, System.Transactions aborts the transaction on a timer, the running batch is not interrupted and runs until its own command timeout, and the error is `TransactionException` with `TimeoutException` inside instead of the timeout error of SQL Server (the shape that 0096-68 verified for `DeploymentTimeout`).

- [ ] How to run the batches one by one: the batch parser that SMO uses (if it is public), or a parser of our own (`GO` alone on its line, `GO <count>`, not inside comments and strings)?
- [ ] The same path serves the other scripts whose timeout is a command timeout: breaking scripts of the Code folder (`CodeItemTimeout`), BeforeDeploy and AfterDeploy (`BeforeDeployFolderTimeout`, `AfterDeployFolderTimeout`, "per file" in the docs), and a migration with `"TransactionWrapLevel": "None"` (its `Timeout`). From the code, not verified: they are limited per batch on SQL Server too. Make their timeouts per file as well?
- [ ] `breaking: true`? A code file whose batches together run longer than `CodeItemTimeout` deployed before and fails now.

## Docs to update
- `docs/llms-full.txt` §6 ("Per-file command timeout"), §9.2 (`CodeItemTimeout`)
- `docs/deployment/code.md` ("Transactions and timeouts")
- `docs/project/configuration.md` ("Timeouts": `CodeItemTimeout`)

## Done when
- [ ] On SQL Server a code file whose batches together run longer than `CodeItemTimeout` fails with the timeout error of SQL Server and its path, and nothing of it is applied; integration test (two batches of 2 s, `CodeItemTimeout` 3 s)
- [ ] A file whose batches together fit into `CodeItemTimeout` deploys as before
- [ ] Docs updated

## Notes
- Came up 2026-11-16 in 0096-83, when the maintainer asked why the transaction of a code file is a transaction of T-SQL and not a `TransactionScope`.
- Running the batches ourselves may also settle the todo in `MsSqlDA.ExecuteScriptAsync`: SMO reports the line number of an error inside its batch, not inside the file.
