using EasyFlow.Tests.Common;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTestingDemo : EasyFlowTesting, IDisposable
{
	readonly static string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTestingDemo);
	readonly static string _projectPath = Path.GetFullPath(@"TestProject_MSSQL");
	readonly static string _dbConnectionString = new TestConfig().GetSqlServerConnectionString(_unitTestsDBName);

	public EasyFlowTestingDemo(ITestOutputHelper output) 
		: base(_projectPath, _dbConnectionString, output)
	{
	}

	public void Dispose()
	{
		//DropTestingDatabase(_unitTestsDBName);
		// TODO: add drop database method to the base class.
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void DB(string test, int num) => RunTest(test, num);

	public static IEnumerable<object[]> GetListOfTests() => GetListOfTestsBase(_projectPath);
}
