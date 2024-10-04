using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL.xunit;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyEasyFlowTestingMSSQLFixture() : EasyFlowTestingMSSQLFixture(Path.GetFullPath(@"DemoMSSQL"))
{ }

public class DBTests(ITestOutputHelper _output, MyEasyFlowTestingMSSQLFixture _fixture)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = "DemoMSSQL")]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);		
	}
}