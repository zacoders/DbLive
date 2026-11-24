---
type: bug
priority: medium
created: 2026-11-16
closed: 2026-11-16
breaking: true
---
# `CodeItemTimeout` limits each `GO` batch on SQL Server, not the code file

## Release note
**SQL Server: the timeout of a script is the timeout of the whole file, not of each `GO` batch.** `CodeItemTimeout`, `MigrationTimeout` and the `Timeout` of a migration, `BeforeDeployFolderTimeout`, `AfterDeployFolderTimeout` and `UnitTestItemTimeout` now limit all batches of a file together, as they already did on PostgreSQL. A file whose batches together run longer than its timeout deployed before and fails now: raise its timeout (for one migration: `Timeout` in its item settings file) or split the file.

**SQL Server: a batch whose connection breaks fails the script.** DbLive runs the `GO` batches itself now, split by SMO as before. SMO, which ran them until now, opened a broken connection again (a killed session, a failover, a network error) and ran the batch once more on a new session, without the session state of the batches before it. A script without a transaction, a code file among them, then ran the statements of that batch twice. Now the script fails with the error, and the next deployment runs it again, as after any other error.

**A timeout is `ExecutionTimeoutException` on both providers.** A script that runs longer than its timeout fails with `ExecutionTimeoutException`, a `DbLiveSqlException` with the limit in `Timeout`, and the message `Execution Timeout Expired. The script ran longer than its timeout of 00:00:30.` It is the inner exception of the deployment error that names the file, and the error of a SQL test that runs too long. Before, it was the error of the provider (`Execution Timeout Expired. The timeout period elapsed ...` on SQL Server, `Exception while reading from stream` / `Timeout during reading attempt` on PostgreSQL). On SQL Server that error is now the inner exception of `ExecutionTimeoutException`; on PostgreSQL DbLive cancels the script, and the inner exception is the cancel, `OperationCanceledException`. Code that looks for these texts should check for `ExecutionTimeoutException` instead.

A timeout under one second now limits the script: on SQL Server to one second, on PostgreSQL to its value; before, it was no limit. `00:00:00` is still no limit.

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

On SQL Server SMO splits the file into batches in its capture mode, and DbLive runs the batches one by one on its connection, with the time left of the file as the command timeout of each. A file that runs out of time fails with `ExecutionTimeoutException` (see the questions below) and the path of the file; the transaction of the file (0096-83) is rolled back.

The same holds for every script that runs through `MsSqlDA.ExecuteScriptAsync`: its timeout is the timeout of the whole file. Besides the code files these are the breaking scripts of the Code folder, BeforeDeploy and AfterDeploy, the migration and breaking scripts of the Migrations folder (with and without a transaction), `setup.test.sql`, and the self-migrations of DbLive (30 seconds, the default of `GetTimeoutSeconds`; now passed explicitly, see the decision below). A SQL test (`ExecuteQueryMultipleAsync`) is one command and gets the same rounding and the same exception.

Decision of the maintainer (2026-11-16): a timeout of `00:00:00` is no limit, as it is now (`CommandTimeout = 0` in SqlClient and in Npgsql).

Not a `TransactionScope` with `CodeItemTimeout` as its timeout: when it times out, System.Transactions aborts the transaction on a timer, the running batch is not interrupted and runs until its own command timeout, and the error is `TransactionException` with `TimeoutException` inside instead of the timeout error of SQL Server (the shape that 0096-68 verified for `DeploymentTimeout`).

- [x] How to run the batches one by one? SMO splits the file in its capture mode, through its public API, the same way it does now (see "Splitting with SMO, running with DbLive" in Notes); DbLive runs the batches. Without SMO the choice would be a parser of our own with the rules of SMO, the lexer of ScriptDom, or the batch parser of sqltoolsservice in source; the splitter of DbUp as it is does not fit.
  - Decision of the maintainer (2026-11-16): the capture mode of SMO, as the agent proposed. The batches stay exactly as they are now, and DbLive gets no parser to keep.
- [x] The same path serves the other scripts whose timeout is a command timeout: breaking scripts of the Code folder (`CodeItemTimeout`), BeforeDeploy and AfterDeploy (`BeforeDeployFolderTimeout`, `AfterDeployFolderTimeout`, "per file" in the docs), and a migration with `"TransactionWrapLevel": "None"` (its `Timeout`). From the code, not verified: they are limited per batch on SQL Server too. Make their timeouts per file as well?
  - Decision of the maintainer (2026-11-16): yes. They all go through `ExecuteScriptAsync`, on PostgreSQL they are per file already, and for BeforeDeploy and AfterDeploy the docs say "per file" now.
- [x] `breaking: true`? A code file whose batches together run longer than `CodeItemTimeout` deployed before and fails now.
  - Decision of the maintainer (2026-11-16): yes, a deployment that passed can fail after the upgrade. The release note tells to raise the timeout of a file whose batches together run longer than it.
- [x] What does DbLive throw when the time of the file runs out between two batches? No batch runs into the timeout of SqlClient then (see "Details of the time left" in Notes). The maintainer (2026-11-16): a timeout of a file must always look the same, not the timeout of SQL Server one time and an error of `CodeItemTimeout` another time; can DbLive throw the timeout of SQL Server itself?
  - Proposal of the agent (2026-11-16): a `DbLiveSqlException` with the message and the `SqlError` that a timeout of SqlClient gives (taken from a real timeout in a test). A `SqlException` itself cannot be made: `SqlException`, `SqlError` and `SqlErrorCollection` are sealed and have no public constructor or factory in SqlClient 6.1.4 (verified by reflection); only reflection over the internals of SqlClient could make one, and that breaks with its updates. It is not needed either: DbLive never throws a bare `SqlException` (`ExecuteScriptAsync` wraps every error into `DbLiveSqlException`, and the deployers wrap that with the path of the file), and nothing in DbLive reads the number of an error to find a timeout (only 4060 in `CheckDatabaseExistsAsync`). The one difference left is the inner exception: the `SqlException` of SqlClient, or none.
  - Decision of the maintainer (2026-11-16): an exception of DbLive, `ExecutionTimeoutException`, for every timeout of a script, which replaces the timeout errors of the providers. It derives from `DbLiveSqlException` (no longer `sealed`), so `catch (DbLiveSqlException)` and the error column keep working. The timeout error of the provider is its inner exception when there is one.
  - Decision of the maintainer (2026-11-16), how DbLive tells a timeout: on SQL Server a `SqlException` with the number -2, on PostgreSQL an `NpgsqlException` with a `TimeoutException` inside, both only around the commands of the script: a connection timeout looks the same. The `statement_timeout` of PostgreSQL (57014) is a setting of the user, not a timeout of DbLive, and stays its own error.
- [x] Where does `ExecutionTimeoutException` replace the errors of the providers?
  - Decision of the maintainer (2026-11-16): the command timeouts of scripts, on both providers: `ExecuteScriptAsync` / `ExecuteNonQueryAsync` and `ExecuteQueryMultipleAsync`. Not the timeouts of a `TransactionScope` (`DeploymentTimeout`, the transaction of a migration and of a test): when such a timer ends the transaction, the error comes from the next step (the next connection, the next batch, the write of a row in `dblive`, the end of the scope) in different shapes, and it does not say which timeout ran out. They are a task of their own: 0096-86, decision of the maintainer (2026-11-16). Not the connection timeout of the connection string, nor the queries of DbLive to its own tables.

## Docs to update
- `docs/llms-full.txt` §5.3 (the timeout of a migration and its error), §6 ("Per-file command timeout"), §6.1 (timeout of a breaking script of the Code folder), §7 (BeforeDeploy and AfterDeploy), §8.2 (a test that runs too long), §9.2 (the `*Timeout` keys), §9.3 (`Timeout`), §10 ("Command timeouts ... on SQL Server to each `GO` batch"), §11.2 (`DbLiveSqlException` of a timeout), §16 (script execution), §17 (`MigrationDeploymentException`, `FolderDeploymentException`, `TransactionAbortedException`, new `ExecutionTimeoutException`)
- `docs/deployment/code.md` ("Transactions and timeouts")
- `docs/deployment/breaking-changes.md` (execution of a breaking script of the Code folder)
- `docs/project/configuration.md` ("Timeouts", "Item settings": `Timeout`)
- `docs/unit-testing/index.md` (a test that runs too long)

## Done when
- [x] On SQL Server a code file whose batches together run longer than `CodeItemTimeout` fails with `ExecutionTimeoutException` and its path, and nothing of it is applied; integration test (batches of 2 s and 2.5 s, `CodeItemTimeout` 3 s; not two of 2 s, see "Implementation" in Notes)
- [x] The time that runs out between two batches gives the same exception, without an inner one; integration test
- [x] A file whose batches together fit into `CodeItemTimeout` deploys as before
- [x] The other scripts of `ExecuteScriptAsync` have their timeout per file too; integration test with one of them (an AfterDeploy file of two batches)
- [x] PostgreSQL: a timeout of a migration, a code file and a SQL test is `ExecutionTimeoutException`; integration tests
- [x] The batches are the ones of SMO: a test of the splitting on the cases of the table in Notes, `GO 3` and empty batches included
- [x] A timeout of `00:00:00` is no limit; the time left is rounded up to whole seconds and never becomes `0` by rounding; unit tests
- [x] Docs updated

## Notes
- Came up 2026-11-16 in 0096-83, when the maintainer asked why the transaction of a code file is a transaction of T-SQL and not a `TransactionScope`.
- The line of an error in the file (the todo in `MsSqlDA.ExecuteScriptAsync`) is a task of its own: 0096-85, decision of the maintainer (2026-11-16).
- The maintainer (2026-11-16): SMO in the project is fine; DbLive stays on it.
- Impact: a code file is mostly `create or alter` of a procedure, a view or a function, and its batches take milliseconds. The timeout matters for a file of several long batches (an index of an indexed view, a data fix in a code file).

### How the file is split into batches now
Verified on 2026-11-16 (SMO 180.10.0, `lib/net8.0`):
- DbLive has no parser of its own and none in the git history. `ServerConnection.ExecuteNonQuery(string)` splits the file with its private method `GetStatements(string, ExecutionTypes, ref int)`, which calls `Microsoft.Data.Tools.Sql.BatchParser.dll`, an assembly of the SMO package.
- All types of that assembly are internal; `InternalsVisibleTo` names only `Microsoft.SqlServer.ConnectionInfo` and `SmoCodeGen`. DbLive cannot call it, and calling `GetStatements` through reflection would tie DbLive to a private method of SMO.
- `GetStatements` returns the texts of the batches only, not where they start in the file.

What `GetStatements` does with `ExecutionTypes.Default` (the type that `ExecuteNonQuery(string)` uses). Verified by a temporary program (reflection, not kept):

| Script | Batches |
| --- | --- |
| `GO` alone on its line, in any case, with spaces around it and `-- comment` after it | a separator; the line of `GO` belongs to no batch |
| `GO 3` | the batch before it, three times |
| `GO;` | not a separator, stays in the batch |
| `select 1 GO` | not a separator |
| `GO` inside `/* */` (also nested `/* /* */ ... */`), `'...'`, `[...]` or `"..."` | not a separator |
| `GO` on the first line, two `GO` in a row | empty batches `""`; `ExecuteNonQuery` does not run a batch with an empty text (from the source) |
| `$(x)`, `:setvar`, `:r` | not processed: sent to the server as they are |
| `/*` without `*/` | `BatchParserException: Missing end comment mark '*/'`, before any batch runs |
| a `GO` line with `/* */`, or a count that is not digits (`GO -1`, `GO 3x`, `GO 3 4`) | `BatchParserException`, before any batch runs; all forms of the `GO` line are in 0096-85 |
| CRLF line ends | the same as LF |

A new way of splitting must give the same batches, or a file that deploys now fails after the upgrade. Test it against this table, and against SMO on the scripts of the demo projects.

### Splitting with SMO, running with DbLive
From the source of SMO (microsoft/sqlmanagementobjects, `ServerConnection.ExecuteNonQuery(string, ExecutionTypes, bool)`, main of 2026-04-08): when `SqlExecutionModes` has no `ExecuteSql`, `ExecuteNonQuery` splits the script with `GetStatements`, adds each batch to `CapturedSql` and runs nothing. `SqlExecutionModes` and `CapturedSql` are public.

```csharp
ServerConnection splitter = new() { SqlExecutionModes = SqlExecutionModes.CaptureSql };
splitter.ExecuteNonQuery(script);
StringCollection batches = splitter.CapturedSql.Text;
```

Verified on 2026-11-16 by a temporary program (SMO 180.10.0, not kept): it needs no connection, and gives the same batches as `GetStatements` on 13 cases (the table above, `GO 3`, CRLF, `use master`), with the same `BatchParserException` for a `/*` without `*/`. DbLive then runs each batch with a non-empty text as a `SqlCommand` on its connection, with the time left of the file as `CommandTimeout`.

What DbLive leaves behind with the execution of SMO (from the source):
- An error comes as `SqlException` itself, not inside `ExecutionFailureException`; `ExecuteScriptAsync` finds it by `Get<SqlException>()` either way.
- The retry after a lost connection. When a batch fails and the connection is closed, `ConnectionManager.TryPrepareConnectionForRetry` opens it again and runs the batch once more. In the transaction of the file (0096-83) the new connection has no transaction: that batch and the next ones commit one by one, and the final `commit transaction` fails, which leaves a part of the file applied. Without the execution of SMO this retry is gone. Verified, see "The retry of SMO after a lost connection".

### The retry of SMO after a lost connection
From the source (microsoft/sqlmanagementobjects, `ConnectionManager.ExecuteTSql` and `TryPrepareConnectionForRetry`): when a batch fails with a `SqlException` and the connection is not open after it, SMO calls `Open()` on the same `SqlConnection` and runs the same batch once more, then goes on with the next batches. The connection it opens is a new session: without the transaction of T-SQL, the `SET` options and the temporary tables of the batches before. When `Open()` throws, the first error stays. It happens on every error that closes the connection: a killed session, a failover or a restart of the server, a network error, an error of severity 20 or more.

Verified on 2026-11-16 with a temporary integration test (SQL Server 2025 in Testcontainers, not kept): a script of three batches, each inserts a row with its batch and `@@spid`; the second one waits after its insert, and another session kills it after 1 s.

| Where the script runs | SMO (DbLive 1.5) | DbLive after this task |
| --- | --- | --- |
| Without a transaction: a migration with `None`, BeforeDeploy, AfterDeploy, a breaking script of the Code folder | No error. The insert of batch 2 ran twice, once on each session; batch 3 ran on the new session | `DbLiveSqlException` "Cannot continue the execution because the session is in the kill state."; batches 1 and 2 stay, batch 3 does not run |
| A code file in its transaction of T-SQL (0096-83) | The kill rolled back batches 1 and 2. SMO ran 2 and 3 again in autocommit, and they stayed committed; batch 1 was lost; `commit transaction` failed: "The COMMIT TRANSACTION request has no corresponding BEGIN TRANSACTION." The file was reported as failed and was applied in part | The same error; nothing of the file stays |
| In a `TransactionScope`: a migration with `Migration` (the default), `TransactionWrapLevel: Deployment`, a SQL test | No retry: `Open()` cannot enlist in the aborted transaction and throws, so the first error stays (from the source and the shape 0096-68 verified; not part of this test) | The error of the batch |

So the retry did not heal a lost connection: it ran a part of the script a second time on another session, silently in a script without a transaction, and in a code file it brought back the partly applied file that 0096-83 fixed. What goes with it: a deployment whose connection breaks while a script without a transaction runs fails now, where before it could pass with a batch run twice; it is run again as after any other error. SqlClient itself still restores a connection that broke while idle before it sends the next command (`ConnectRetryCount`), outside a transaction: from its documentation, not verified here.

### How DbUp does it
dbup-sqlserver (`SqlConnectionManager.SplitScriptIntoCommands`) splits a script with `SqlCommandSplitter` of dbup-core (`SqlCommandReader` on `SqlParser`, MIT), and `ScriptExecutor` runs each part as a command of its own. `WithExecutionTimeout` ("the execution timeout for scripts") is the `CommandTimeout` of each command: per batch, like DbLive now. dbup-sqlserver logs an error with the number of the batch and the line inside it (`Script block number: {0}; Block line {1}`), not the line in the file.

Its splitter compared with SMO on the same scripts, verified on 2026-11-16 by a temporary program (package dbup-core 6.1.1; `SqlParser` has not changed since 2024; not kept). The same in both: `/* */` with nesting, `--`, `'...'` with `''`, `[...]`, any case, CRLF, `$(x)` left as it is. Different:

| Script | SMO | DbUp |
| --- | --- | --- |
| `select 1 as go` + line end + `from t`; `select go from t` | one batch | split at `go`: `select 1 as` and `from t` (a `GO` between two white spaces is a separator anywhere) |
| `select 1 GO` + line end | one batch | two batches |
| `GO 3` | the batch three times | `3` starts the next batch: a syntax error |
| `GO;` | not a separator | a separator |
| `GO` inside `"..."` | not a separator | a separator |
| `-- comment` after `GO` | dropped | starts the next batch (harmless) |
| `/*` without `*/` | an error before any batch runs | one batch, the server reports the error |
| `GO` on the first line, two `GO` in a row | empty batches | skipped |
| text of a batch | as in the file | trimmed: without the empty lines at its start, the line of an error inside it is shifted |

So the splitter of DbUp as it is would split some files that deploy now in a different way. As a base its scanner of comments, strings and brackets fits; the rule of `GO` has to change (alone on its line, `GO <count>`), `"..."` and the position of each batch have to be added. Code taken from it needs the MIT notice of DbUp.

### Candidates for the parser
- SMO in its capture mode (see above): the batches of today, no code to keep.
- Our own: a scanner of `/* */` with nesting, `--`, `'...'`, `[...]` and `"..."`, and lines `GO [count]`. It knows the line where each batch starts. The scanner of DbUp can be its base (see above).
- The lexer of ScriptDom (`Microsoft.SqlServer.TransactSql.ScriptDom`, MIT): one more package. Not checked whether it reads `GO 3`.
- The source of the same parser: the types of `Microsoft.Data.Tools.Sql.BatchParser` (`BatchParser`, `Lexer`, `Parser`, `TextBlock`, `LineInfo`) have the names of `ManagedBatchParser` in microsoft/sqltoolsservice (MIT), probably the same code (not checked). It is much larger, and it also handles sqlcmd commands, which SMO leaves to the server.

### Details of the time left
- `CommandTimeout` is whole seconds, and `0` means no limit. The time left is rounded up and never becomes `0` by rounding.
- A timeout of `00:00:00` is no limit: no time left is counted, every batch gets `CommandTimeout = 0`. A timeout under one second (`00:00:00.5`) is no limit now as well (`(int)TotalSeconds` is `0`); rounded up it becomes 1 s.
- The time can run out between two batches: the rounding up lets a batch finish after the end of the time of the file. Then no batch runs into the timeout of SqlClient, and DbLive throws `ExecutionTimeoutException` itself, without an inner exception (decision above). Running the next batch with 1 s instead does not limit the file: every fast batch after it would get its own second.
- `GO 3`: each repetition is a batch of its own and gets the time left.
- Empty batches are skipped: `SqlCommand` with an empty `CommandText` throws `InvalidOperationException`.

### Considered and not taken
- Keep SMO, and cancel the running batch with `ServerConnection.Cancel()` (public in 180.10.0) when the time of the file runs out. The error is "Operation cancelled by user", not the timeout of SQL Server. A `Cancel` between two batches cancels nothing, and the next batch runs with the whole `StatementTimeout`.
- Keep the execution of SMO, and set `StatementTimeout` to the time left in a handler of the event `StatementExecuted`. From the source: despite its name the event is raised before the batch runs, after `GetSqlCommand` has already copied `StatementTimeout` into the command of that batch. A new value reaches only the next batch, and it is computed before the current one runs.

### Implementation
- Verified on 2026-11-16 by a temporary program (SMO 180.10.0, not kept): `ServerConnection` is not `IDisposable`; the capture mode gives `[]` for an empty script, a batch of white space as it is (`"   \n"`, and `"\n"` after the last `GO` of `select 1` + `GO` + an empty line), and no batch after a `GO` at the end of the file. A `BatchParserException` comes as it is, not inside `ExecutionFailureException`. DbLive skips the batches of white space too: the server does nothing with them.
- `waitfor delay '00:00:02'` ends up to 0.15 s early in the SQL Server 2025 container: verified on 2026-11-16 with a temporary test (not kept), it ended after 1.85 s. The next batch then gets 1.13 s, rounded up to 2 s, so two batches of 2 s run to their end with `CodeItemTimeout` 3 s, and the file deploys after 3.7 s. That is the rounding up, not an error: a batch may end up to a second after the time of the file, and no batch starts after it. The integration test therefore has a last batch of 2.5 s, longer than any time left it can get. The same test verified that SqlClient keeps a `CommandTimeout` of 1 s exactly (-2 after 1.01 s), also on a connection that ran a command before.
- After the last batch DbLive does not check the time: the file has run to its end, and a script without a transaction has committed its batches already. A file of one batch is limited by its rounded-up command timeout, as a file on PostgreSQL is.
- The clock of a file starts right before its first batch, after the connection is open and `begin transaction` has run, as the `CommandTimeout` of PostgreSQL starts with its command.
- The deployers catch `DbLiveSqlException` and need no change: `ExecutionTimeoutException` has an empty `SqlError`, so the error of the file is its message on both providers.
- Decision of the maintainer (2026-11-16): no default timeout. `ScriptTimeout` has none, and `ExecuteNonQueryAsync`, `ExecuteQueryMultipleAsync` and `ExecuteCodeItemAsync` of `IDbLiveDA` take the isolation level and the timeout without defaults (`TimeSpan`, not `TimeSpan?`): every caller says them. The self-deployer passes `Serializable`, the level of its transaction, and 30 seconds, the timeout its migrations had by default, as named constants. The integration tests run their own SQL (arranging data, reading what a deployment left) through `ExecuteSqlAsync` and `QuerySqlAsync` of a test helper with `ReadCommitted` and 30 seconds; a test of the data access itself passes them explicitly.

## Result
- New public `ExecutionTimeoutException : DbLiveSqlException` (`Timeout`; message `Execution Timeout Expired. The script ran longer than its timeout of <timeout>.`); `DbLiveSqlException` is no longer `sealed`. Internal `ScriptTimeout` counts the time of a script and gives the command timeout of its next command: the time left rounded up to whole seconds, `0` (no limit) for `00:00:00`, and `ExecutionTimeoutException` when no time is left.
- `MsSqlDA.ExecuteScriptAsync`: SMO splits the file in its capture mode (`SplitIntoBatches`), before the connection is opened; DbLive runs each batch as a `SqlCommand` on its connection, with the time left of the file as its `CommandTimeout`, inside the transaction of the file (0096-83) when it has one. A `SqlException` with the number -2 of a batch becomes `ExecutionTimeoutException`. SMO no longer runs batches, so its retry of a batch after a lost connection is gone: a broken connection fails the script, and no batch runs twice (verified, see "The retry of SMO after a lost connection"). `ExecuteQueryMultipleAsync` on both providers and `PostgreSqlDA.ExecuteNonQueryAsync` take the command timeout from `ScriptTimeout` too, and turn the timeout of the provider into `ExecutionTimeoutException`. The `GetTimeoutSeconds` of both providers is gone.
- No default timeout or isolation level: the script methods of `IDbLiveDA` require both, `DbLiveSelfDeployer` passes `Serializable` and 30 seconds as named constants, and the integration tests use the helpers `ExecuteSqlAsync` / `QuerySqlAsync` (`DbLiveDATestExtensions`) for their own SQL.
- Timeouts of a `TransactionScope` are not part of this task (decision above): 0096-86.
- No change to the `dblive` tables.
- Docs: `docs/llms-full.txt` (§5.3, §6, §6.1, §7, §8.2, §9.2, §9.3, §10, §11.2, §16, §17 with a row for `ExecutionTimeoutException`), `docs/deployment/code.md`, `docs/deployment/breaking-changes.md`, `docs/project/configuration.md`, `docs/unit-testing/index.md`.
- Unit tests: `ScriptTimeoutTests` (rounding up, `00:00:00`, under one second, no time left, a script that just started, the exception with and without the error of the provider), `DbLiveSelfDeployerTests` (a self-migration runs with `Serializable` and 30 seconds), `MsSqlDABatchesTests` (the cases of the table in Notes: `GO` in any case with spaces and a comment, `GO 3`, empty batches, CRLF, `GO;`, `GO` in comments, strings and brackets, sqlcmd commands, `BatchParserException`). The PostgreSQL shape of a lost connection replaces that of a timeout in `FolderDeployerTests` and `MigrationItemDeployerTests`.
- Integration tests, SQL Server (`CommandTimeoutTests`): a code file of batches of 2 s and 2.5 s with `CodeItemTimeout` 3 s fails with `ExecutionTimeoutException` (inner `SqlException` -2), no view left, row `error`; a file whose time runs out between two batches fails the same way without an inner exception; batches of 1 s and 1 s deploy within 3 s; an AfterDeploy file of two batches fails with `FolderDeploymentException`; a migration and a SQL test give `ExecutionTimeoutException`. PostgreSQL (`CommandTimeoutTests`): a migration, a SQL test and a code file of two `pg_sleep(2)` with 3 s give `ExecutionTimeoutException` (inner `NpgsqlException` with `TimeoutException`).
- All tests pass: `DbLive.Tests` 524, `DbLive.MSSQL.Tests` 115, `DbLive.PostgreSQL.Tests` 61.
