using EasyFlow.MSSQL;
using EasyFlow.Tests.Common;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTestingMSSQLFixture : TheoryData<string>
{
	readonly static string UnitTestsDBName = $"EasyFlow-UnitTests-{nameof(EasyFlowTestingDemo)}-{Guid.NewGuid()}";
	readonly static string DBConnectionString = new TestConfig().GetSqlServerConnectionString(UnitTestsDBName);
	readonly static string ProjectPath = Path.GetFullPath(@"TestProject_MSSQL");

	readonly EasyFlowTestingMSSQL testingMSSQL = new(ProjectPath, DBConnectionString);

	public EasyFlowTestingMSSQLFixture()
	{
		foreach (var testItem in testingMSSQL.TestsList)
		{
			Add(testItem.Key); // adding tests to TheoryData base class.
		}
	}

	public void RunTest(ITestOutputHelper output, string relativePath)
	{
		testingMSSQL.RunTest(output.WriteLine, relativePath);
	}
}

public class EasyFlowTestingDemo(EasyFlowTestingMSSQLFixture _fixture, ITestOutputHelper _output)
	: IClassFixture<EasyFlowTestingMSSQLFixture>
{
	[Theory]
	[ClassData(typeof(EasyFlowTestingMSSQLFixture))]
	public void Sql(string relativePath) => _fixture.RunTest(_output, relativePath);
}
