using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using EasyFlow.Common;
using EasyFlow.Deployers;
using EasyFlow.Project;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTesting
{
	protected readonly IServiceCollection Container;

	private readonly TestItem[] TestsList;
	private readonly IUnitTestsRunner _unitTestsRunner;
	private readonly string _sqlConnectionString;

	public ITestOutputHelper Output { get; }

	public EasyFlowTesting(string _msSqlTestingProjectPath, string dbConnectionString, ITestOutputHelper output)
	{
		var project = new EasyFlowProject(new FileSystem());
		project.Load(_msSqlTestingProjectPath);
		TestsList = [.. project.GetTests()];

		Container = new ServiceCollection();
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

		_sqlConnectionString = dbConnectionString;

		easyFlow.DeployProject(_msSqlTestingProjectPath, _sqlConnectionString, deployParams);
		Output = output;
	}

	protected TService GetService<TService>()
	{
		return Container.BuildServiceProvider().GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}

	public void RunTest(string test, int num)
	{
		Output.WriteLine($"Running unit test #{num}: {test}");

		var testItem = TestsList[num];

		var testRunResult = _unitTestsRunner.RunTest(testItem, _sqlConnectionString, new EasyFlowSettings());

		Output.WriteLine(testRunResult.Output);

		Assert.True(testRunResult.IsSuccess, testRunResult.ErrorMessage);
	}

	/// <summary>
	/// Returns list of tests. Static method since it is used in the [MemberData] attribute.
	/// </summary>
	/// <returns></returns>
	public static IEnumerable<object[]> GetListOfTestsBase(string projectPath)
	{
		//yield return new object[] { "", -1 };

		var project = new EasyFlowProject(new FileSystem());
		project.Load(projectPath);

		int indexer = 0;
		foreach (var testItem in project.GetTests())
		{
			yield return new object[] { testItem.Name, indexer++ };
		}
	}
}
