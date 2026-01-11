using DbLive.Deployers.Testing;
using DbLive.xunit.SqlTest;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;

public class MySqlTestFixture()
	: DbLiveTestFixture(
		fixtureBuilder: new DockerPostgresFixtureBuilder(),
		dropDatabaseOnComplete: true
	  )
{
}

public class SqlTests(ITestOutputHelper _output, MySqlTestFixture _fixture)
	: IClassFixture<MySqlTestFixture>
{
	[SqlFact(TestFixture = typeof(MySqlTestFixture))]
	public async Task Sql(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath).ConfigureAwait(false);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}
