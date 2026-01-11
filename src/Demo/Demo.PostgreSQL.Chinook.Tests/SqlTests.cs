using DbLive.Deployers.Testing;
using DbLive.xunit;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;

public class SqlTests(ITestOutputHelper _output, MyPostgreSQLFixture _fixture)
	: IClassFixture<MyPostgreSQLFixture>
{
	[SqlFact(TestFixture = typeof(SqlTests))]
	public async Task Sql(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath).ConfigureAwait(false);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}
