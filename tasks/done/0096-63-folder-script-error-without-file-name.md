---
type: bug
priority: low
created: 2026-11-08
closed: 2026-11-08
breaking: true
---
# The error of a BeforeDeploy or AfterDeploy script does not name the file

## Release note
A failed BeforeDeploy or AfterDeploy script now fails the deployment with `FolderDeploymentException` (namespace `DbLive.Deployers.Folder`). Its message names the file and contains the SQL error, `RelativePath` holds the file, and the inner exception is the `DbLiveSqlException`. Before, the `DbLiveSqlException` came out alone, with only the SQL error, and the file was found only in the `Information` log. To upgrade: code or a test that catches or expects `DbLiveSqlException` from `DeployAsync` for these scripts catches `FolderDeploymentException` instead, and reads `InnerException` for the SQL details.

## Problem
`FolderDeployer` does not wrap the error of a script: `DbLiveSqlException` leaves the deployment with the SQL error only, for example "Invalid object name 'dbo.X'.", without the path of the file. Migrations (`MigrationDeploymentException`) and code files (`CodeDeploymentException`) name the file. With several scripts in `AfterDeploy` the file is found only by the `Information` line "Deploying item: ..." before the error, which is not there with a higher log level.

Found by the code review of 2026-11-08 (finding 6).

## Where
- `src/DbLive/Deployers/Folder/FolderDeployer.cs:42-51`

## Fix
Proposal: catch the error of the script and throw an exception that names the file and has the error as its inner exception, like the other deployers.

- [x] A new exception type (`FolderDeploymentException`?) or an existing one? A user who catches `DbLiveSqlException` of these folders would see another type: `breaking: true`?
  Maintainer, 2026-11-08: a new type `FolderDeploymentException`, `breaking: true`.

## Docs to update
- `docs/llms-full.txt` §7, §17
- `docs/project/structure.md` ("BeforeDeploy and AfterDeploy")

## Done when
- [x] Fixed, with a unit test
- [x] Docs updated

## Notes
- Options weighed: a new type (chosen); a new `DbLiveSqlException` with the file in the message (keeps the type, but an adapter exception wrapping itself, unlike the other deployers); a subclass of `DbLiveSqlException` (`Assert.ThrowsAsync<DbLiveSqlException>` checks the exact type and fails anyway); only an `Error` log line with the file (the exception alone, as CI shows it, still has no file).
- Nothing above `FolderDeployer` wraps the error: `DbLiveDeployer` calls it directly for both folders. Both adapters wrap every exception of `ExecuteNonQueryAsync` in `DbLiveSqlException`, so catching that type is enough.
- Releases of #96 already have `breaking: true` tasks, so one more does not change the version.
- The `dblive` tables do not change: `MinCompatibleVersion` stays.

## Result
- New `FolderDeploymentException(relativePath, error, innerException)` in `DbLive.Deployers.Folder`, with `RelativePath`. The message has the format of `MigrationDeploymentException`: `Error deploying <path> file.` and on the next line `SqlError`, or the message of the `DbLiveSqlException` when `SqlError` is empty (a command timeout).
- `FolderDeployer` catches `DbLiveSqlException` of the script, logs it at `Error` with the file, and throws `FolderDeploymentException` with it as the inner exception. The timeout is chosen before the `try`, so an unknown folder still fails with `ArgumentOutOfRangeException`. `MarkItemAsAppliedAsync` stays outside the `try`.
- Tests: `FolderDeployerTests.Script_error_names_the_file_and_stops_the_folder` (the message, `RelativePath`, the inner exception; the next script does not run, the failed one is not recorded) and `Script_error_without_sql_error_puts_the_exception_message_in_the_message`. All 452 tests of `DbLive.Tests` pass.
- Docs: `docs/llms-full.txt` §7, §17; `docs/project/structure.md`.
