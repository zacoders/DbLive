using DbLive.Deployers.Testing;
using DbLive.xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;

public class DBTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture _fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = MyDbLiveTestingMSSQLFixture.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}