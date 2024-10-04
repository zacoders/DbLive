using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL.xunit;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public static class TestConstants
{
	public const string SqlProjectName = "DemoMSSQL";
}

public class MyEasyFlowTestingMSSQLFixture() 
	: EasyFlowTestingMSSQLFixture(TestConstants.SqlProjectName)
{ 
}

public class DBTests(ITestOutputHelper _output, MyEasyFlowTestingMSSQLFixture _fixture)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = TestConstants.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);		
	}
}