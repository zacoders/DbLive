# DbLive.xunit

xUnit v2 integration for [DbLive](https://github.com/zacoders/DbLive):

- `[SqlFact]` turns every SQL test file of a DbLive project into its own test case in Visual Studio Test Explorer and `dotnet test`.
- `[SqlDeployFact]` deploys the whole project to an empty database the way production does (the release, the breaking phase, the breaking phase again; the SQL tests run after the first two) and runs every undo script before and after the breaking phase. It compares the schema: an undo must restore it, and a breaking script must not change it on a second run.
- `LogToXUnitOutput(ITestOutputHelper)` sends DbLive logs to the test output.

```csharp
public class MyDbTestFixture()
    : DbLiveTestFixture(fixtureBuilder: new MyFixtureBuilder(), dropDatabaseOnComplete: true) { }

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
