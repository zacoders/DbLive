# DbLive.xunit

xUnit v2 integration for [DbLive](https://github.com/zacoders/DbLive):

- `[SqlFact]` turns every SQL test file of a DbLive project into its own test case in Visual Studio Test Explorer and `dotnet test`.
- `[SqlDeployFact]` deploys the whole project to an empty database the way production does (the release, the breaking phase, the breaking phase again; the SQL tests run after the first two) and runs every undo script before and after the breaking phase. It compares the schema: an undo must restore it, and a breaking script must not change it on a second run.
- `LogToXUnitOutput(ITestOutputHelper)` sends DbLive logs to the test output.

The fixture gets its database from a fixture builder: `MsSqlContainerFixtureBuilder` or `PostgreSqlContainerFixtureBuilder` for a server in Docker (packages `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`), `MsSqlFixtureBuilder` or `PostgreSqlFixtureBuilder` for a server you run (in the provider packages). Every fixture gets a new database with a random name.

```csharp
public class MyDbTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);

public class SqlTests(ITestOutputHelper output, MyDbTestFixture fixture) : IClassFixture<MyDbTestFixture>
{
    [SqlFact(TestFixture = typeof(MyDbTestFixture))]
    public async Task Sql(string testFileRelativePath)
    {
        TestRunResult result = await fixture.Tester!.RunTestAsync(output.WriteLine, testFileRelativePath);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
```

- Unit testing guide: https://zacoders.github.io/DbLive/unit-testing/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
