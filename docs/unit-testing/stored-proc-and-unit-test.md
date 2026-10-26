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

Run the console app. The output now contains the code deployment and the test run:

```
[23:15:02 INF] Deploying code.
[23:15:02 INF] Deploying code file: Code\dbo.GetUser.sql
[23:15:02 INF] Code deploy successfully completed.
...
[23:15:02 INF] Running Tests.
[23:15:02 INF] PASSED: dbo.GetUser.test.sql
[23:15:02 INF] PASSED: dbo.GetUser.not-found.test.sql
[23:15:02 INF] Tests Run Result> Passed: 2, Failed: 0.
```

Run it again: the procedure is skipped (`[hash match]`) because the file did not change, and the tests run again.

## 4. Run the tests from Visual Studio

Running every test through a deployment is fine for CI, but while you work on SQL you want each test in Test Explorer.

1. Add an xUnit test project next to `MyDb` and reference `MyDb`:

    ```bash
    dotnet new xunit -n MyDb.Tests
    dotnet add MyDb.Tests reference MyDb
    dotnet add MyDb.Tests package DbLive.xunit
    dotnet add MyDb.Tests package DbLive.MSSQL
    ```

    `DbLive.xunit` works with xUnit v2. If the template created an xUnit v3 project, switch it to the `xunit` 2.x package.

2. Add a fixture builder that points to a test server. This one uses a local SQL Server and creates a new database for every test run:

    ```csharp
    using DbLive;
    using DbLive.MSSQL;
    using DbLive.xunit;
    using System.Reflection;

    public class LocalSqlServerFixtureBuilder : IDbLiveFixtureBuilder
    {
        public Task<DbLiveBuilder> GetBuilderAsync()
        {
            string connectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
                .SetRandomMsSqlDatabaseName();

            return Task.FromResult(new DbLiveBuilder()
                .SqlServer()
                .SetDbConnection(connectionString)
                .SetProject(GetProjectAssembly(), useVsProjectPath: true));
        }

        public Assembly GetProjectAssembly() => Assembly.Load("MyDb");
    }
    ```

3. Add the fixture and the test class:

    ```csharp
    using DbLive.Deployers.Testing;
    using DbLive.xunit.SqlTest;
    using Xunit;
    using Xunit.Abstractions;

    public class MyDbTestFixture()
        : DbLiveTestFixture(fixtureBuilder: new LocalSqlServerFixtureBuilder(), dropDatabaseOnComplete: true)
    {
    }

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

4. Build and open Test Explorer. Each `.test.sql` file is a separate test named after its path. Because the builder uses `useVsProjectPath: true`, edits to the SQL files are picked up the next time you run a test, without a rebuild.

To run the same tests against SQL Server in Docker, replace the builder with the Testcontainers one from [Unit Testing](index.md#running-tests-in-visual-studio-and-ci-xunit).

## Next steps

- [Unit Testing](index.md): all assertions, `init.sql`, deployment tests
- [Code Deployment](../deployment/code.md): ordering, retries, versioned procedures
- [Project Configuration](../project/configuration.md): `settings.json`
