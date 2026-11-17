# Add a Stored Procedure and a Unit Test

This tutorial continues [Getting Started](../gettingstarted.md). You have a console project, here `MyDb`, with a `Scripts` folder and a migration that creates `dbo.Users (UserId, Name)`. Now you add a stored procedure and two SQL tests for it, run the tests in Visual Studio, and see what a failing test looks like.

## 1. Add the procedure

Create `Scripts/Code/dbo.GetUser.sql`:

```sql
create or alter proc dbo.GetUser
    @UserId int
as
    select UserId, Name
    from dbo.Users
    where UserId = @UserId
go
```

Files in `Code` are deployed after migrations and redeployed whenever they change, so they use `create or alter`.

## 2. Add the tests

Create `Scripts/Code/dbo.GetUser.test.sql` next to it. The `.test.sql` suffix makes it a test instead of a code object.

```sql
-- Arrange
set identity_insert dbo.Users on;
insert into dbo.Users (UserId, Name) values (1000, 'test-user');
set identity_insert dbo.Users off;

-- Act
exec dbo.GetUser @UserId = 1000;

-- Assert
select assert = 'rows';
select 1000 as UserId, 'test-user' as Name;
```

`select assert = 'rows'` splits the output: the result sets before it are the actual result, the ones after it the expected result. The test runs in a transaction that is rolled back, so the inserted user never stays in the database. Do not use `GO` in test files. The details are in [Anatomy of a test](index.md#anatomy-of-a-test).

Add a second test for the "not found" case, `Scripts/Code/dbo.GetUser.not-found.test.sql`:

```sql
exec dbo.GetUser @UserId = -1;
select assert = 'empty';
```

## 3. Deploy

Run the console app. The output now contains the code deployment:

```
[23:15:02 INF] Deploying code.
[23:15:02 INF] Deploying code file: Code/dbo.GetUser.sql
[23:15:02 INF] Code deploy successfully completed.
```

Run it again: the procedure is skipped (`[hash match]`) because the file did not change. A deployment does not run the tests; the next step does.

## 4. Run the tests

Create the test project `MyDb.Tests` with the fixture and the `[SqlFact]` test class as described in [Running tests in Visual Studio and CI](index.md#running-tests-in-visual-studio-and-ci-xunit). Without Docker, use the `MsSqlFixtureBuilder` line from [A fixture builder](index.md#1-a-fixture-builder): it points to your local SQL Server.

Build and open Test Explorer. It shows one test per file, named after its path: `Code/dbo.GetUser.test.sql` and `Code/dbo.GetUser.not-found.test.sql`. Run them; both pass. The fixture deploys `MyDb` into a new database before the tests and drops it afterwards, so the tests never touch the database of your console app.

## 5. Break the procedure

Change `Scripts/Code/dbo.GetUser.sql` to return the name in upper case:

```sql
    select UserId, upper(Name) as Name
```

Run the tests again. You do not need to run the console app: Test Explorer builds first, and the fixture deploys the changed procedure. `Code/dbo.GetUser.test.sql` fails with:

```
Data for one or more rows does not match:
Columns: UserId: int,Name: varchar(9)
[Row 1]>
Expected values: 1000, 'test-user'
Actual values:   1000, 'TEST-USER'
```

`rows` compares strings exactly, including case ([How values are compared](index.md#how-values-are-compared)). Change it back, and the test passes again.

## Next steps

- [Unit Testing](index.md): all assertions, `setup.test.sql`, deployment tests
- [Code Deployment](../deployment/code.md): ordering, retries, versioned procedures
- [Project Configuration](../project/configuration.md): `settings.json`
