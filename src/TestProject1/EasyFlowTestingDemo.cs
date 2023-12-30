using EasyFlow.Tests.Common;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTestingMSSQLFixture : EasyFlowTestingMSSQL
{
	readonly static string _unitTestsDBName = $"EasyFlow-UnitTests-{nameof(EasyFlowTestingDemo)}-{Guid.NewGuid()}";
	readonly static string _dbConnectionString = new TestConfig().GetSqlServerConnectionString(_unitTestsDBName);
	internal readonly static string _projectPath = Path.GetFullPath(@"TestProject_MSSQL");

	public EasyFlowTestingMSSQLFixture() 
		: base(_projectPath, _dbConnectionString)
	{
	}
}

public class EasyFlowTestingDemo(EasyFlowTestingMSSQLFixture _mssql, ITestOutputHelper _output) 
	: IClassFixture<EasyFlowTestingMSSQLFixture>
{
	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void Sql(string test) => _mssql.RunTest(_output, test);

	public static IEnumerable<object[]> GetListOfTests() => EasyFlowTesting.GetListOfTestsBase(EasyFlowTestingMSSQLFixture._projectPath);
}
