using EasyFlow;
using EasyFlow.Project.Settings;
using EasyFlow.Tests;
using EasyFlow.VSTests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : DeploySqlIntegrationBaseTest, IDisposable
{
	readonly string _unitTestsDBName = GetRanomDbName();

	private string SqlConnectionString => $"Data Source=.;Initial Catalog={_unitTestsDBName};Integrated Security=True;TrustServerCertificate=True;";

	private static readonly EasyFlowPrepareTests TestsPrepare = new (DBEngine.MSSQL, _msSqlTestingProjectPath);

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
		// preparing sql database for unit testing.

		var deploy = GetService<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, SqlConnectionString, DeployParameters.Default);

		deploy.DeployProject(_msSqlTestingProjectPath, SqlConnectionString, DeployParameters.Breaking);
	}

	public void Dispose()
	{
		DropTestingDatabases(_unitTestsDBName);
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void RunTests(string testName, int testIndex)
	{
		Output.WriteLine($"Running unit test #{testIndex}: {testName}");

		var testItem = TestsPrepare.TestItems[testIndex];

		var testRunResult = TestsPrepare.Run(testItem, SqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess);
	}

	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { "", -1 };

		int indexer = 0;
		foreach (var testItem in TestsPrepare.TestItems)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
