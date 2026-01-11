using DbLive.Deployers.Testing;
using DbLive.xunit;
using Xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;

public class DBTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture _fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	[SqlFact(TestFixture = typeof(MyDbLiveTestingMSSQLFixture))]
	public async Task SqlAsync(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath).ConfigureAwait(false);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}