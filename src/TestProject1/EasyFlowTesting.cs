using EasyFlow;
using EasyFlow.Project.Settings;
using EasyFlow.Tests;
using EasyFlow.VSTests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	readonly static string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTesting);

	readonly static string _sqlConnectionString = GetDbConnectionString(_unitTestsDBName);

	private static readonly EasyFlowPrepareTests TestsPrepare = new(DBEngine.MSSQL, _msSqlTestingProjectPath);

	public void Dispose()
	{
		//DropTestingDatabase(_unitTestsDBName);
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void DB(string test, int num)
	{
		Output.WriteLine($"Running unit test #{num}: {test}");

		var testItem = TestsPrepare.TestItems[num];

		var testRunResult = TestsPrepare.Run(testItem, _sqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { "", -1 };
		TestsPrepare.PrepareUnitTestingDatabase(_sqlConnectionString);

		int indexer = 0;
		foreach (var testItem in TestsPrepare.TestItems)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
