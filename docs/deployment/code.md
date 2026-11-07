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
    select Id, UserName from dbo.Users where Deleted = 0
go
```

```sql
-- Code/dbo/GetUsers.sql, this commit
create or alter proc dbo.GetUsers_v2 as
    select Id, UserName from dbo.Users where Deleted = 0 and Active = 1
go
```

```sql
-- Code/dbo/GetUsers.breaking.sql, this commit
drop proc if exists dbo.GetUsers_v1;
```

After deployment the database contains both versions: old application instances call `_v1`, new ones call `_v2`. The breaking phase drops `_v1` once the old application is gone (see [Dropping an old procedure version](breaking-changes.md#dropping-an-old-procedure-version)). Code is deployed by state: to make `_v1` the current version again, revert `GetUsers.sql` in git and deploy a new release; `_v2` stays. See [Roll back a release](migrations.md#roll-back-a-release).

When only the implementation changes and the contract stays the same, keep the name: the object is redeployed and its callers do not notice. See [Database as a Service](../database-as-service.md#a-procedure-is-a-module-with-a-history) and [Zero-Downtime Deployment](zero-downtime.md).

## Order, parallelism and retries

- Subfolders listed in `CodeSubFoldersDeploymentOrder` are deployed first, one group after another, in the listed order. Everything else forms the last group. Nested subfolders such as `"dbo/functions"` work with either `/` or `\`.
- Within a group, up to `NumberOfThreadsForCodeDeploy` files (10 by default) are deployed in parallel.
- A file that fails goes to the end of the queue and is retried, up to `MaxCodeDeployRetries` attempts (5 by default). Dependencies therefore usually resolve themselves: a view that uses a function deployed later succeeds on its second attempt.
- On PostgreSQL with `TransactionWrapLevel: Deployment`, an error aborts the deployment transaction, so a failed file is not retried: the deployment fails at once with the error of that file. Order dependencies with `CodeSubFoldersDeploymentOrder` there.
- When a file still fails after the last attempt, the deployment stops with `CodeDeploymentAggregateException`, which contains the SQL errors.

Use the ordering setting for objects that everything else depends on, so that you do not spend retries on them:

```json
{ "CodeSubFoldersDeploymentOrder": [ "schemas", "types" ] }
```

## Transactions and timeouts

Each code file runs as its own command, without a transaction, with a timeout of `CodeItemTimeout` (30 seconds by default; on SQL Server per `GO` batch). With `TransactionWrapLevel: Deployment`, code runs single-threaded inside the deployment transaction.

On PostgreSQL a file with several statements runs as one implicit transaction, so it fails or succeeds as a whole. With `Deployment` on PostgreSQL, the first failing file aborts the deployment transaction and a retry cannot succeed: order the dependencies with `CodeSubFoldersDeploymentOrder`.

## Things to know

- **Manual changes are not detected.** If someone alters a procedure directly in the database, the file's checksum still matches and DbLive will not redeploy it. Change the file, or delete its row from `dblive.code`, to force a redeploy. Paths are stored there in lower case: `delete from dblive.code where relative_path = lower('Code/dbo/GetUser.sql');`.
- **One object per file** keeps redeployments small and errors easy to locate. On SQL Server, a file may contain several batches separated by `GO`.
- **Status** of every file is in `dblive.code` (`applied`, `error`, or `removed` for a file no longer in the project), together with the breaking scripts of the Code folder (`item_type = 'breaking'`); the last error of a failed file is in its `error` column. With `TransactionWrapLevel: Deployment` a failed deployment is rolled back and leaves no `error` row: the error is in the exception and the log.
