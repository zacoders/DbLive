using EasyFlow;
using EasyFlow.Project.Settings;
using EasyFlow.Tests;
using EasyFlow.VSTests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : DeploySqlIntegrationBaseTest, IDisposable
{
	readonly string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTesting);

	private string SqlConnectionString => $"Data Source=.;Initial Catalog={_unitTestsDBName};Integrated Security=True;TrustServerCertificate=True;";

	private static readonly EasyFlowPrepareTests TestsPrepare = new (DBEngine.MSSQL, _msSqlTestingProjectPath);

	public EasyFlowTesting(ITestOutputHelper output) : base(output)
	{
		// preparing sql database for unit testing.

		if (!DbExists(SqlConnectionString))
		{
			var deploy = GetService<IEasyFlow>();

			DeployParameters deployParams  = new()
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = false /* we will run tests in Visual Studio UI */
			};

			deploy.DeployProject(_msSqlTestingProjectPath, SqlConnectionString, deployParams);
		}
	}

	public void Dispose()
	{
		//DropTestingDatabase(_unitTestsDBName);
	}

	[Theory]
	[MemberData(nameof(GetListOfTests))]
	public void RunTests(string testName, int testIndex)
	{
		Output.WriteLine($"Running unit test #{testIndex}: {testName}");

		var testItem = TestsPrepare.TestItems[testIndex];

		var testRunResult = TestsPrepare.Run(testItem, SqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
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
