using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public class MyEasyFlowTestingMSSQLFixture()
	: EasyFlowTestingMSSQLFixture(Path.GetFullPath(@"DemoMSSQL"))
{ }

public class MyEasyFlowTestingMSSQLTheoryData()
	: EasyFlowTestingMSSQLTheoryData(Path.GetFullPath(@"DemoMSSQL"))
{ }

public class EasyFlowTestingDemo(MyEasyFlowTestingMSSQLFixture _fixture, ITestOutputHelper _output)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	[Theory]
	[ClassData(typeof(MyEasyFlowTestingMSSQLTheoryData))]
	public void Sql(string relativePath)
	{
		_fixture.EasyFlowTester!.RunTest(_output.WriteLine, relativePath);
	}
}
