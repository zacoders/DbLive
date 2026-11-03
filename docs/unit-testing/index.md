# Unit Testing

DbLive tests are plain `.sql` files with Arrange, Act and Assert sections. Every test runs inside a transaction that is **always rolled back**, so tests need no cleanup and can run again and again.

The tests run as normal xUnit tests in Visual Studio Test Explorer and in `dotnet test`, one test case per file (package `DbLive.xunit`). You need no shared test database: the test fixture deploys your scripts into a new database, in Docker (Testcontainers) or on a local server, runs the tests against it and drops it at the end. SQL tests then work like ordinary C# unit tests: every developer and every CI run gets the same database, built from the same scripts.

A deployment never runs the tests, so a production deployer does not. Without xUnit v2, run them all with one call ([Running all tests without xUnit](#running-all-tests-without-xunit)).

Tutorial: [Add a Stored Procedure and a Unit Test](stored-proc-and-unit-test.md).

## Where tests live

| Location | Which files are tests |
|----------|-----------------------|
| `Tests` and its subfolders | every `.sql` file except `setup.test.sql` |
| `Code` and its subfolders | files matching `TestFilePatterns`: `test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql` by default, except `setup.test.sql` |

Keep a test next to the object it tests (`Code/dbo/GetUser.sql` and `Code/dbo/GetUser.test.sql`). Use `Tests` for checks that span several objects or verify data rules.

## Anatomy of a test

```sql
-- Arrange
set identity_insert dbo.Users on;
insert into dbo.Users (UserId, Name) values (10, 'TestUser10');
set identity_insert dbo.Users off;

-- Act
exec dbo.GetUser @UserId = 10;

-- Assert
select assert = 'rows';
select 10 as UserId, 'TestUser10' as Name;
```

The **assert result set** is a result set whose first column is named `assert` (in any case). Its value chooses the assertion. Result sets returned **before** it are the actual results; result sets **after** it are the expected results. Statements that return no result set (`insert`, `set`, `declare`) are ignored.

A misspelled column (`select asstert = 'rows'`) or a missing alias (`select 'rows'`) does not silently turn the assertion off: the test fails with a message that names the column.

!!! warning "No `GO` in SQL Server test files"
    A test file is executed as a single batch, so `GO` is a syntax error there. `setup.test.sql`, migrations and code files may use `GO`.

## Assertions

| Assert | Passes when |
|--------|-------------|
| `rows` | There are as many actual result sets as expected ones, and each expected result set matches the actual result set at the same position: same column names in the same order, same number of rows, same values row by row. |
| `rows-with-schema` | Like `rows`, and the SQL column types match as well (for example `nvarchar(255)` vs `nvarchar(100)` fails). |
| `has-rows` | The first actual result set has at least one row. |
| `row-count=N` | There is exactly one actual result set, with exactly N rows. |
| `empty` | Shortcut for `row-count=0`. |
| `single-row` | Shortcut for `row-count=1`. |
| _(none)_ | The test passes if the script runs without an error. |

Only one assert result set is allowed per test. Assert values are case-insensitive.

### Custom checks

Without an assert result set, raise an error to fail the test:

```sql
-- SQL Server
declare @cnt int = (select count(*) from dbo.Users where Name = 'Admin');
if @cnt != 1 throw 50000, 'Expected one admin user.', 0;
```

```sql
-- PostgreSQL
do $$
begin
    if (select count(*) from users where name = 'Admin') != 1 then
        raise exception 'Expected one admin user.';
    end if;
end $$;
```

### How values are compared

`rows` compares each value by its JSON representation:

- `int` 10 and `bigint` 10 are equal.
- `decimal` values must have the same scale: `150.00` is not `150` and not `150.0`. Cast the expected value, for example `cast(150 as decimal(10,2))`.
- Strings are compared exactly, including case and trailing spaces.
- Compare a date or time value with a value of the same type, not with a string: cast the expected value, `cast('2026-01-01' as date)`. On PostgreSQL a `timestamptz` column equals only a `timestamptz` value.
- `NULL` equals `NULL`.
- Row order matters; use `order by` in the act.
- Column names must match exactly. SQL Server gives an unnamed expression an empty name, so `select dbo.GetFullName('a', 'b')` can be compared with `select cast('a b' as nvarchar(255))`. PostgreSQL names such columns (`?column?` or the function name), so give both sides an alias.

!!! note "`rows` compares all actual output"
    `rows` fails on extra or missing result sets and rows, for example `Expected 1 row(s), but 3 received.` Every result set returned before the assert counts as actual, including output from the Arrange part (`update ... output`, a debugging `select`). Remove such output, or move the setup into `setup.test.sql`.

`row-count` counts rows of the actual result set: `select count(*) from dbo.Orders` returns **one** row. To check that there are three orders, select the orders themselves:

```sql
select OrderId from dbo.Orders;
select assert = 'row-count=3';
```

## Shared setup with `setup.test.sql`

A `setup.test.sql` file runs before **each** test in the same folder, inside the test's transaction. It works in `Tests` and in `Code`:

```text
Tests/
  Orders/
    setup.test.sql           inserts three orders
    select-orders.test.sql
    update-order.test.sql
Code/
  dbo/
    GetUser.sql
    GetUser.test.sql
    setup.test.sql           inserts test users
```

- It applies to tests in its own folder only, not to subfolders.
- It runs on a separate connection within the same transaction. Rows it inserts are visible to the test; session state is not (temporary tables, variables, `SET` options).
- It is never a test itself and never deployed as code, whatever `TestFilePatterns` contains.

## Isolation and parallelism

- Each test runs in its own transaction with `TestsTransactionIsolationLevel` (default `Serializable`) and a timeout of `UnitTestItemTimeout` (default 1 minute). The transaction is rolled back after the assertion, whatever the result.
- The [deployment test](deployment-tests.md) and `RunAllTestsAsync()` run all tests in parallel (`NumberOfThreadsForTestsRun`, default 10). Tests that write to the same tables under `Serializable` can deadlock; lower the thread count to 1 or use `ReadCommitted` if that happens.

## Running tests in Visual Studio and CI (xUnit)

`DbLive.xunit` (xUnit v2) turns every SQL test file into an xUnit test case. For a new solution, `dotnet new dblive` creates the SQL project together with this test project, its fixture and both test classes ([Getting Started](../gettingstarted.md#with-the-template)). To add the tests to an existing SQL project, create a test project next to it (here `MyDb`) and reference it:

```bash
dotnet new xunit -n MyDb.Tests
dotnet add MyDb.Tests reference MyDb
dotnet add MyDb.Tests package DbLive.xunit
dotnet add MyDb.Tests package DbLive.Testcontainers.MSSQL   # SQL Server in Docker
```

- The `xunit` template adds `Microsoft.NET.Test.Sdk`, `xunit` and `xunit.runner.visualstudio`. `DbLive.xunit` works with xUnit v2; if the template created an xUnit v3 project, switch it to the `xunit` 2.x package.
- For PostgreSQL in Docker, add `DbLive.Testcontainers.PostgreSQL`. To test against a server you run, add the provider package (`DbLive.MSSQL` or `DbLive.PostgreSQL`) instead and use its builder from the table below.

### 1. A fixture builder

The builder gives the fixture its database. Each builder takes the SQL project assembly and gives every fixture a new database with a random name (`DbLive--<guid>` on SQL Server, `dblive--<guid>` on PostgreSQL), so fixtures that run in parallel never share a database:

| Builder | Package (namespace `DbLive.MSSQL` / `DbLive.PostgreSQL`) | Database |
|---------|---------|----------|
| `MsSqlContainerFixtureBuilder(project, image?)` | `DbLive.Testcontainers.MSSQL` | SQL Server in Docker |
| `PostgreSqlContainerFixtureBuilder(project, image?)` | `DbLive.Testcontainers.PostgreSQL` | PostgreSQL in Docker |
| `MsSqlFixtureBuilder(project, connectionString)` | `DbLive.MSSQL` | A SQL Server you run |
| `PostgreSqlFixtureBuilder(project, connectionString)` | `DbLive.PostgreSQL` | A PostgreSQL server you run |

```csharp
// SQL Server in Docker
new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb"))

// Without Docker, a local server; the database name in the connection string is replaced
new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;")
```

- `Assembly.Load("MyDb")` loads the SQL project by its assembly name, so the test project must reference it. A type from it works too: `typeof(MyDbMarker).Assembly`.
- The container builders start one container per test run with [Testcontainers](https://dotnet.testcontainers.org/). The first fixture starts it; fixtures that start at the same time wait for that start and share the container. Testcontainers removes it when the test run ends. Docker must be running.
- The default images are `mcr.microsoft.com/mssql/server:2025-latest` and `postgres:latest`. Pass another one as the second argument: `new PostgreSqlContainerFixtureBuilder(project, "postgres:17")`. Each image gets its own container.
- For a database set up in another way (a server from the configuration, a container with other options), implement `IDbLiveFixtureBuilder` (namespace `DbLive`). `GetBuilderAsync()` is called once per fixture and returns a `DbLiveBuilder` with the provider, the connection string of a new database and the project. `GetProjectAssembly()` returns the SQL project assembly; test discovery reads the tests from it. The ready-made builders are a few lines each, for example [MsSqlFixtureBuilder](https://github.com/zacoders/DbLive/blob/main/src/DbLive.MSSQL/MsSqlFixtureBuilder.cs).

### 2. SQL tests: `[SqlFact]`

```csharp
using DbLive.Deployers.Testing;
using DbLive.MSSQL;
using DbLive.xunit.SqlTest;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

public class MyDbTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);

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

- The fixture class must derive from `DbLiveTestFixture` and have a public parameterless constructor.
- The fixture deploys the project once per test class (migrations, code and breaking changes, no tests) into a fresh database and drops it at the end if `dropDatabaseOnComplete` is true.
- Each SQL file appears in Test Explorer under its relative path; "Go to source" opens the `.sql` file (it needs the project folder that the [DbLive package](../project/index.md#embedding-the-scripts) records in the SQL project's assembly).
- The tests read the scripts embedded in the SQL project's dll. Test Explorer and `dotnet test` build before running, so changed scripts are picked up; new test files appear after a build.

![Visual Studio Tests](../visual-studio-tests.png)

### Deployment tests

`[SqlDeployFact]` deploys the project to an empty database the way production does (the release, the breaking phase, the breaking phase again), runs every undo script before and after the breaking phase, and checks that each undo restores the schema. It takes the same fixture builders: [Deployment Tests](deployment-tests.md).

## Running all tests without xUnit

`[SqlFact]` and `[SqlDeployFact]` need xUnit v2. With another test framework (NUnit, MSTest, xUnit v3), or to test a database that is already deployed, such as a local or a QA database, run all tests with the tester of a builder, for example in one test of your framework:

```csharp
DbLiveBuilder builder = new DbLiveBuilder()
    .SqlServer()
    .SetDbConnection(connectionString)
    .SetProject(typeof(MyDbMarker).Assembly);

await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
await builder.CreateTester().RunAllTestsAsync();
```

`RunAllTestsAsync` does not deploy; against a database that is already deployed, leave out the `DeployAsync` line. Do not call it in a production deployer (see [Isolation and parallelism](#isolation-and-parallelism)).

The [fixture builders](#1-a-fixture-builder) do not depend on xUnit. They give such a test a new database too, in Docker or on your server; the database does not exist yet, so the deployment creates it:

```csharp
DbLiveBuilder builder = await new MsSqlContainerFixtureBuilder(typeof(MyDbMarker).Assembly).GetBuilderAsync();

await builder.CreateDeployer().DeployAsync(new DeployParameters { CreateDbIfNotExists = true });
await builder.CreateTester().RunAllTestsAsync();
await builder.CreateDbLiveDA().DropDbAsync();
```

Before the first test, `RunAllTestsAsync` checks the database and runs no test if a check fails:

- The database exists. Otherwise `DatabaseNotFoundException`, which names the database and the server.
- The DbLive tables exist. Otherwise `DatabaseNotDeployedException`: the project was never deployed there, or the connection string points to another database.
- The [ProjectId](../features/project-id.md) matches. The check only compares: with a `ProjectId` in `settings.json`, a database with another value or with none fails with `ProjectIdMismatchException`. A deployment of the project stores the value, so this stops only tests run against a database the project was not deployed to.
- The isolation levels in `settings.json` are supported by the database engine, as at the start of a deployment.

Results are saved in `dblive.unit_test_run`. If any test fails, `RunAllTestsAsync` throws `UnitTestsFailedException` after all tests have run, so the calling test fails and a console app exits with a non-zero code. The exception has `FailedCount`, `TotalCount` and `FailedTests` (relative paths of the failed test files). The deployment was a separate call and stays committed.
