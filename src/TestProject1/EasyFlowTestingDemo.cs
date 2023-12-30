using EasyFlow.Tests.Common;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTestingDemo : EasyFlowTestingMSSQL
{
	readonly static string _unitTestsDBName = $"EasyFlow-UnitTests-{nameof(EasyFlowTestingDemo)}-{Guid.NewGuid()}";
	readonly static string _projectPath = Path.GetFullPath(@"TestProject_MSSQL");
	readonly static string _dbConnectionString = new TestConfig().GetSqlServerConnectionString(_unitTestsDBName);

	public EasyFlowTestingDemo(ITestOutputHelper output)
		: base(_projectPath, _dbConnectionString, output)
	{		
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void DB(string test, int num) => RunTest(test, num);

	public static IEnumerable<object[]> GetListOfTests() => EasyFlowTesting.GetListOfTestsBase(_projectPath);
}
