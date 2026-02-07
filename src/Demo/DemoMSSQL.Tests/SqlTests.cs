using DbLive.Deployers.Testing;
using DbLive.xunit.SqlTest;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyDbLiveTestingMSSQLFixture()
	: DbLiveTestFixture(
		fixtureBuilder: new DockerMsSqlFixtureBuilder(),
		dropDatabaseOnComplete: true
	  )
{
}

public class DBTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture _fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	[SqlFact(TestFixture = typeof(MyDbLiveTestingMSSQLFixture))]
	public async Task SqlAsync(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}