# Add a Stored Procedure and a Unit Test

This tutorial continues [Getting Started](../gettingstarted.md). You have a console project with a `Scripts` folder and a migration that creates `dbo.Users (UserId, Username)`. Now you add a stored procedure, a SQL test for it, and an xUnit project that shows the test in Visual Studio.

## 1. Add the procedure

Create `Scripts/Code/dbo.GetUser.sql`:

```sql
create or alter proc dbo.GetUser
    @UserId int
as
    select UserId, Username
    from dbo.Users
    where UserId = @UserId
go
```

Files in `Code` are deployed after migrations and redeployed whenever they change, so they use `create or alter`.

## 2. Add the test

Create `Scripts/Code/dbo.GetUser.test.sql` next to it. The `.test.sql` suffix makes it a test instead of a code object.

```sql
-- Arrange
set identity_insert dbo.Users on;
insert into dbo.Users (UserId, Username) values (1000, 'test-user');
set identity_insert dbo.Users off;

-- Act
exec dbo.GetUser @UserId = 1000;

-- Assert
select assert = 'rows';
select 1000 as UserId, 'test-user' as Username;
```

Everything before `select assert = ...` is the actual result; everything after it is the expected result. The test runs in a transaction that is rolled back, so the inserted user never stays in the database. Do not use `GO` in test files.

Add a second test for the "not found" case, `Scripts/Code/dbo.GetUser.not-found.test.sql`:

```sql
exec dbo.GetUser @UserId = -1;
select assert = 'empty';
```

## 3. Deploy

Run the console app. The output now contains the code deployment:

```
[23:15:02 INF] Deploying code.
[23:15:02 INF] Deploying code file: Code\dbo.GetUser.sql
[23:15:02 INF] Code deploy successfully completed.
```

Run it again: the procedure is skipped (`[hash match]`) because the file did not change. A deployment does not run the tests; the next step does.

## 4. Run the tests

The SQL tests run as ordinary xUnit tests: each `.test.sql` file is a test case in Visual Studio Test Explorer and in `dotnet test`.

1. Add an xUnit test project next to `MyDb` and reference `MyDb`:

    ```bash
    dotnet new xunit -n MyDb.Tests
    dotnet add MyDb.Tests reference MyDb
    dotnet add MyDb.Tests package DbLive.xunit
    dotnet add MyDb.Tests package DbLive.MSSQL
    ```

    `DbLive.xunit` works with xUnit v2. If the template created an xUnit v3 project, switch it to the `xunit` 2.x package.

2. Add the fixture and the test class. The fixture builder `MsSqlFixtureBuilder` points to your local SQL Server and gives the fixture a new database with a random name; the database name in the connection string is replaced:

    ```csharp
    using DbLive.Deployers.Testing;
    using DbLive.MSSQL;
    using DbLive.xunit.SqlTest;
    using System.Reflection;
    using Xunit;
    using Xunit.Abstractions;

    public class MyDbTestFixture()
        : DbLiveTestFixture(
            new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"),
            dropDatabaseOnComplete: true);

    public class SqlTests(ITestOutputHelper output, MyDbTestFixture fixture)
        : IClassFixture<MyDbTestFixture>
    {
        [SqlFact(TestFixture = typeof(MyDbTestFixture))]
        public async Task Sql(string testFileRelativePath)
        {
            TestRunResult result = await fixture.Tester!.RunTestAsync(output.WriteLine, testFileRelativePath);
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }
    }
    ```

3. Build and open Test Explorer. Each `.test.sql` file is a separate test named after its path. The tests read the scripts embedded in the `MyDb` dll; Test Explorer builds before running, so edits to the SQL files are picked up the next time you run a test. Before the tests, the fixture deploys `MyDb` into a new `DbLive--<guid>` database on that server, and drops it afterwards, so the tests never touch your development database.

To run the same tests against SQL Server in Docker, add the package `DbLive.Testcontainers.MSSQL` to `MyDb.Tests` and replace the builder with `new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb"))` ([Unit Testing](index.md#1-a-fixture-builder)).

## Next steps

- [Unit Testing](index.md): all assertions, `setup.test.sql`, deployment tests
- [Code Deployment](../deployment/code.md): ordering, retries, versioned procedures
- [Project Configuration](../project/configuration.md): `settings.json`
