using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using EasyFlow.Common;
using EasyFlow.Deployers;
using EasyFlow.Project;
using EasyFlow.Tests;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting : SqlServerIntegrationBaseTest, IDisposable
{
	readonly static string _unitTestsDBName = "EasyFlow-UnitTests-" + nameof(EasyFlowTesting);

	private static TestItem[] TestsList;
	private IUnitTestsRunner _unitTestsRunner;
	private string _sqlConnectionString;

	static EasyFlowTesting()
	{
		var project = new EasyFlowProject(new FileSystem());
		project.Load(_msSqlTestingProjectPath);
		TestsList = project.GetTests().ToArray();
	}

	public EasyFlowTesting(ITestOutputHelper output)
		: base(output)
	{
		Container.InitializeMSSQL();
		Container.InitializeEasyFlow();

		var easyFlow = GetService<IEasyFlow>();
		_unitTestsRunner = GetService<IUnitTestsRunner>();

		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false /* we will run tests in Visual Studio UI */
		};

		_sqlConnectionString = GetDbConnectionString(_unitTestsDBName);

		easyFlow.DeployProject(_msSqlTestingProjectPath, _sqlConnectionString, deployParams);
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

		var testItem = TestsList[num];

		var testRunResult = _unitTestsRunner.RunTest(testItem, _sqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	/// <summary>
	/// Returns list of tests. Must be static member since it is used in the [MemberData] attribute.
	/// </summary>
	/// <returns></returns>
	public static IEnumerable<object[]> GetListOfTests()
	{
		//yield return new object[] { "", -1 };

		int indexer = 0;
		foreach (var testItem in TestsList)
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
