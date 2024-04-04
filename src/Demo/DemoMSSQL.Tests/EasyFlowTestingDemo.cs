using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL.xunit;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public class MyEasyFlowTestingMSSQLFixture()
	: EasyFlowTestingMSSQLFixture(Path.GetFullPath(@"DemoMSSQL"))
{ }

public class EasyFlowTestingDemo(MyEasyFlowTestingMSSQLFixture _fixture, ITestOutputHelper _output)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	// TODO: if there are a lot of tests they will be in the same place
	// it will be good to separate them by folders, it can be done by adding filter and creating multiple test methods.
	[Theory]
	[ClassData(typeof(MyEasyFlowTestingMSSQLFixture))]
	public void Sql(string relativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, relativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}
