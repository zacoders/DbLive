using DbLive.Deployers.Testing;
using DbLive.MSSQL;
using DbLive.xunit.SqlTest;
using System.Reflection;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyDbLiveTestingMSSQLFixture()
	: DbLiveTestFixture(
		fixtureBuilder: new MsSqlContainerFixtureBuilder(Assembly.Load("DemoMSSQL")),
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