using EasyFlow;
using EasyFlow.Project.Settings;
using EasyFlow.Tests;
using EasyFlow.VSTests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : DeploySqlIntegrationBaseTest, IDisposable
{
	readonly static string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTesting);

	private static string SqlConnectionString => $"Data Source=.;Initial Catalog={_unitTestsDBName};Integrated Security=True;TrustServerCertificate=True;";

	private static readonly EasyFlowPrepareTests TestsPrepare = new(DBEngine.MSSQL, _msSqlTestingProjectPath);

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
	}

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

		var testRunResult = TestsPrepare.Run(testItem, SqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { "", -1 };
		TestsPrepare.PrepareUnitTestingDatabase(SqlConnectionString);

		int indexer = 0;
		foreach (var testItem in TestsPrepare.TestItems)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}

}
