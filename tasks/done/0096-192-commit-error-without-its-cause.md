---
type: bug
priority: medium
created: 2026-11-26
closed: 2026-11-26
---
# An error at the commit of a script is reported without its cause

## Release note
**An error at the commit of a migration names its cause.** A PostgreSQL error raised when the transaction of a migration or breaking script commits, such as a `deferrable initially deferred` foreign key that the script violates, gave the message `Error deploying Migrations/002.migration.sql file. The transaction has aborted.`; the error of the server was only in the inner exception and in the log. The message now has the error of the server on the next line: `23503: insert or update on table "child" violates foreign key constraint ...`.

## Problem
A script that runs in a transaction of its own (`TransactionWrapLevel: Migration`) commits in the `Dispose()` of its `TransactionScope` (`src/DbLive/Common/Transactions.cs:28`). Npgsql reports a PostgreSQL error at that `COMMIT` as `TransactionAbortedException("The transaction has aborted.")` with the `PostgresException` inside. `ExceptionExtensions.GetSqlErrorOrMessage` (`src/DbLive/Common/ExceptionExtensions.cs`) took the message of the outer exception only, so the `MigrationDeploymentException` said `The transaction has aborted.` and nothing more, while §5.3 and §17 of `docs/llms-full.txt` say that its message contains the SQL error. Verified on PostgreSQL 18 with a migration that inserts a row violating a deferred foreign key. The state was right: the migration is not `applied`, nothing is committed.

A deferred constraint is ordinary PostgreSQL design; SQL Server has none. Code files are not affected: on PostgreSQL they commit with `commit` of their own, and the error is a `DbLiveSqlException`.

With `TransactionWrapLevel: Deployment` the commit is that of the whole deployment, and `DeployAsync` throws the `TransactionAbortedException` itself, with the error of the server inside; there is no file to name. Left as it is.

## Fix
`GetSqlErrorOrMessage` of a `TransactionAbortedException` with an inner exception gives the message of the outer one and, on the next line, the error of the inner one.

## Docs to update
- `docs/llms-full.txt` §17 (`MigrationDeploymentException`): the inner exception of an error at the commit.

## Done when
- [x] Fixed, with a unit test and a PostgreSQL test
- [x] Docs updated

## Notes
- Found by the final code review of 2026-11-26 (the branch as release 2.0), verified by experiment on PostgreSQL 18.
- The same review found that `docs/project/index.md` and §3.2 of `docs/llms-full.txt` called the one-project layout "what the demos use": AdventureWorks is a class library with only scripts, deployed by its test project. Both now name `dotnet new dblive`, DemoMSSQL and Demo.PostgreSQL.Chinook.

## Result
`ExceptionExtensions.GetSqlErrorOrMessage` unwraps a `TransactionAbortedException`: `The transaction has aborted.` and the error inside it, which is the error of the server. Only migration and breaking scripts of `Migrations` commit in a `TransactionScope` of their own; BeforeDeploy and AfterDeploy scripts and the breaking scripts of the Code folder have none, and code files commit with a command. Tests: `MigrationItemDeployerTests.Error_at_the_commit_puts_the_error_inside_the_aborted_transaction_in_the_message` (unit) and `ItemTransactionSettingsTests.Migration_that_violates_a_deferred_constraint_fails_with_the_error_of_the_commit` (PostgreSQL 18: the message, the inner `PostgresException`, nothing committed, the migration not applied).
