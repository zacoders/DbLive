# Code Deployment

Code objects are the **interface** of the database: procedures, functions, views, triggers, and also schemas and other objects that can be (re)created at any time. Unlike migrations, code files are not versioned by name. A file holds the current definition of one object, and git keeps its history.

## How it works

- Every `*.sql` file under `Code` that is not a [test file](../project/structure.md#test-files) and not a [breaking script](breaking-changes.md#dropping-an-old-procedure-version) (`breaking.sql`, `b.sql`, `*.breaking.sql`, `*.b.sql`) is a code item, identified by its path relative to `Scripts`, ignoring case (for example `Code/dbo/GetUser.sql`).
- DbLive executes a code file when the database has no successful run of that path with the same content. Unchanged files are skipped and logged as `[hash match]`.
- A file is executed again when its content changes, when it is moved or renamed, and on every new database. A rename that changes only case (`GetUser.sql` to `getUser.sql`) is not a change. A file that DbLive 1.x deployed from a Windows checkout (CRLF line endings) runs once more after the upgrade to 2.0. **Code files must be re-runnable.**
- Code runs after migrations, so it can use the tables they create.

| Engine | Re-runnable pattern |
|--------|---------------------|
| SQL Server | `create or alter proc / function / view / trigger` |
| PostgreSQL | `create or replace function / view`; `drop function if exists f(args);` followed by `create function` when the signature or the return type changes |

On PostgreSQL write the body of a function or procedure as a string (`as $$ ... $$`) or, for one expression, as `return <expression>`. The SQL-standard body `begin atomic ... end` (PostgreSQL 14) cannot be deployed, in a code file or in any other script: Npgsql splits a script into statements at each `;` outside quotes, comments and parentheses, also inside that body. The file fails on every deployment with `42601: syntax error at end of input`, and DbLive adds a line that names `begin atomic` to the error. `pg_dump` scripts a function created with `begin atomic` with that body: rewrite it when you move existing functions into `Code`.

```sql
-- Code/dbo/GetUser.sql (SQL Server)
create or alter proc dbo.GetUser_v2
    @UserId int
as
    select UserId, Name
    from dbo.Users
    where UserId = @UserId
go
```

```sql
-- Code/schemas/sales.sql (SQL Server): schemas need an existence check
if schema_id('sales') is null
    exec('create schema sales');
```

## Code is never dropped

DbLive does not drop objects. When you delete a file, rename an object in its file, or move the file, the old object stays in the database. That is deliberate: old application instances may still call it, and DBAs may create objects outside DbLive. Remove superseded objects explicitly in a breaking script of the Code folder, next to the file ([Dropping an old procedure version](breaking-changes.md#dropping-an-old-procedure-version)):

```sql
-- Code/dbo/GetUser.breaking.sql
drop proc if exists dbo.GetUser_v1;
```

When code is deployed, the row of a file deleted from the project is marked `removed` in `dblive.code`. The log lists these files with the paths stored in `dblive.code`, in lower case (`code/dbo/getuser.sql`): the files are gone, so their real names are not known. If a later release brings the file back (a `git revert` of its deletion), it is executed again even if its content did not change, because the breaking phase may have dropped its object in between. So the object it creates is restored.

## Versioned interface

When the contract of an object changes (parameters, result columns, meaning), keep the file and change the object name:

```sql
-- Code/dbo/GetUsers.sql, earlier commit
create or alter proc dbo.GetUsers_v1 as
    select UserId, Name from dbo.Users where Deleted = 0
go
```

```sql
-- Code/dbo/GetUsers.sql, this commit
create or alter proc dbo.GetUsers_v2 as
    select UserId, Name from dbo.Users where Deleted = 0 and Active = 1
go
```

```sql
-- Code/dbo/GetUsers.breaking.sql, this commit
drop proc if exists dbo.GetUsers_v1;
```

After deployment the database contains both versions: old application instances call `_v1`, new ones call `_v2`. The breaking phase drops `_v1` once the old application is gone (see [Dropping an old procedure version](breaking-changes.md#dropping-an-old-procedure-version)). Code is deployed by state: to make `_v1` the current version again, revert `GetUsers.sql` in git and deploy a new release; `_v2` stays. See [Roll back a release](migrations.md#roll-back-a-release).

When only the implementation changes and the contract stays the same, keep the name: the object is redeployed and its callers do not notice. See [Database as a Service](../database-as-service.md#a-procedure-is-a-module-with-a-history) and [Zero-Downtime Deployment](zero-downtime.md).

## Order, parallelism and retries

- Subfolders listed in `CodeSubFoldersDeploymentOrder` are deployed first, one group after another, in the listed order: a group starts when the group before it has finished. Everything else forms the last group. Nested subfolders such as `"dbo/functions"` work with either `/` or `\`.
- Within a group, files are deployed in rounds. Up to `NumberOfThreadsForCodeDeploy` files (10 by default) of a round run in parallel. The files that failed are retried in the next round, which starts when every file of the round before has finished.
- Dependencies within a group therefore resolve themselves: a view that uses a function deployed later succeeds in the next round, even when the function takes long to deploy. A file that needs a file of a later group fails.
- When a round deploys no file, the files left fail for another reason than the order, and the deployment stops with `CodeDeploymentAggregateException`, which contains their SQL errors. There is no limit of attempts to configure: every round but the last deploys a file. Retries do not cover transient errors: a file that fails on a deadlock or a lock timeout is retried only when another file of its round succeeded.
- A file that ended the transaction it ran in with `COMMIT` or `ROLLBACK` is not retried, with every `TransactionWrapLevel`: it does so on every run. Neither is a SQL Server file that left a transaction open, `BEGIN TRANSACTION` without `COMMIT` (see [No transaction left open](migrations.md#no-transaction-left-open)): DbLive rolls back the transaction of the file, with `TransactionWrapLevel: Deployment` the deployment transaction. With `Deployment` it does so also when the file fails with an error inside a transaction it began (`begin tran; exec dbo.Missing;`): SQL Server cannot roll back the transaction of the file alone, so the file is not retried either (`Deploy code file error. File path: <path>. The file failed inside a transaction that it began, and DbLive rolled back the deployment transaction with it, so it is not retried.`).
- With `TransactionWrapLevel: Deployment`, a file whose error ended the deployment transaction is not retried: the deployment fails at once with the error of that file. On SQL Server most errors leave the transaction open, but any run-time error with `XACT_ABORT ON` rolls it back (a missing dependency included), and so do a failed conversion and a deadlock. Order dependencies with `CodeSubFoldersDeploymentOrder` there, and leave `set xact_abort on` out of code files: it applies to the rest of the file that sets it, not to the scripts after it. On PostgreSQL every error aborts the transaction, but each code file runs under a savepoint: a failed file is rolled back to it and retried, as without a transaction. On both, a broken connection and the end of `DeploymentTimeout` end the transaction too; after `DeploymentTimeout` the deployment fails with `ExecutionTimeoutException`, and the error of the file is inside it.

Use the ordering setting for objects that everything else depends on, so that the files that use them need no retry:

```json
{ "CodeSubFoldersDeploymentOrder": [ "schemas", "types" ] }
```

## Transactions and timeouts

Each code file runs in a transaction of its own, so it is applied as a whole or not at all. On SQL Server its `GO` batches run between `begin transaction` and `commit`, and a batch that fails rolls back the batches before it. On PostgreSQL the file is one command, which DbLive runs between `begin` and `commit` too. A file that ends this transaction itself with `COMMIT` or `ROLLBACK` fails at once and is not retried (`Deploy code file error. File path: <path>. The file ended its transaction, so it is not retried.`): what it ran after the end is rolled back, what its `COMMIT` committed stays. See [No COMMIT or ROLLBACK in a script](migrations.md#no-commit-or-rollback-in-a-script). The timeout is `CodeItemTimeout` (30 seconds by default) for the whole file, on SQL Server for all its `GO` batches together; the transaction has none of its own. A file that runs longer fails with `ExecutionTimeoutException` (`Execution Timeout Expired. The script ran longer than its timeout of 00:00:30.`), and none of its batches stays applied. See [Timeouts](../project/configuration.md#timeouts).

Statements that SQL Server does not allow in a transaction, such as `ALTER DATABASE` and full-text DDL, fail in a code file, and so do statements that PostgreSQL does not allow in a transaction block, such as `create index concurrently`, `vacuum`, or a `do` block or `call` that commits. Put them into a migration with `"TransactionWrapLevel": "None"` in its [item settings file](../project/configuration.md#item-settings). An `ALTER DATABASE` option that needs the database to itself, such as `READ_COMMITTED_SNAPSHOT`, also takes `WITH ROLLBACK IMMEDIATE`: see the same page.

In-Memory OLTP objects are the exception on SQL Server: a natively compiled procedure, function or trigger, or a memory-optimized table type, can be a code file. SQL Server does not allow their DDL in a transaction, so DbLive runs a file that has the word `native_compilation` or `memory_optimized` without a transaction of its own, as DbLive 1.5 ran every code file, and says so in the log. The word counts anywhere in the file, also in a comment, but only as a whole word: `is_memory_optimized` does not. The batches of such a file commit one by one, so keep the object alone in its file. When such a file fails in part, its row in `dblive.code` stays `applying`: the next deployment runs it again, also when its previous content comes back (`git revert`, a redeploy of the previous release), and that content restores the object. Write the file so that it runs again from any batch it stopped at (`create or alter`, `drop ... if exists`). A `drop` of such an object without either word, or DDL in dynamic SQL, still fails: keep the `drop` in the file that creates the object, or put it into a [breaking script](breaking-changes.md) of the Code folder. With `TransactionWrapLevel: Deployment` such a file fails, as before.

With `TransactionWrapLevel: Deployment`, code runs single-threaded inside the deployment transaction instead. On PostgreSQL a file then runs under a savepoint, and a failed attempt is rolled back to it. Each such savepoint is a subtransaction: a deployment that runs more than 64 code files (attempts included) in one transaction overflows the subtransaction cache of its session, and while it runs, other sessions on a busy server and its replicas check row visibility more slowly. On SQL Server see the retries above for the errors that end the transaction.

## Things to know

- **Manual changes are not detected.** If someone alters a procedure directly in the database, the file's checksum still matches and DbLive will not redeploy it. Change the file, or delete its row from `dblive.code`, to force a redeploy. Paths are stored there in lower case: `delete from dblive.code where relative_path = lower('Code/dbo/GetUser.sql');`.
- **One object per file** keeps redeployments small and errors easy to locate. On SQL Server, a file may contain several batches separated by `GO`.
- **Status** of every file is in `dblive.code` (`applied`, `applying`, or `removed` for a file no longer in the project), together with the breaking scripts of the Code folder (`item_type = 'breaking'`). Before the code files run, DbLive marks the rows of the changed files `applying`, and a successful run sets `applied`. A failed file writes nothing there: its row stays `applying`, with the checksum of its last success, and the next deployment runs the file again whatever its content, also after `git revert` or a redeploy of the previous release. A failed run may have changed the database: a file that committed before its row was written (a broken connection, a stopped process), an In-Memory OLTP file applied in part.
- **Errors** are in the exception and the log. The message of the `CodeDeploymentException` of a failed file names the file on its first line and holds the error of the server on the next lines, without stack traces. On SQL Server the line of an error counts from the start of its `GO` batch, and a line above the errors names the line of the file where the batch starts: `The batch starts at line 4 of the file.`
