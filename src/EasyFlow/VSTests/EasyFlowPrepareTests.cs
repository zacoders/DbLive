namespace EasyFlow.VSTests;

public class EasyFlowPrepareTests
{
	private static IServiceCollection Container = new ServiceCollection();
	private readonly string _projectPath;

	private IUnitTestsRunner UnitTestsRunner { get; init; }
	private IEasyFlow EasyFlow { get; set; }

	public TestItem[] TestItems { get; private set; } = [];

	public EasyFlowPrepareTests(DBEngine dBEngine, string projectPath)
	{
		_projectPath = projectPath;

		Container.InitializeEasyFlow(dBEngine);
		var serviceProvider = Container.BuildServiceProvider();

		UnitTestsRunner = serviceProvider.GetService<IUnitTestsRunner>()!;
		EasyFlow = serviceProvider.GetService<IEasyFlow>()!;

		var project = serviceProvider.GetService<IEasyFlowProject>()!;
		project.Load(projectPath);
		TestItems = [.. project.GetTests()];

	}

	public TestRunResult Run(TestItem testItem, string sqlConnectionString, EasyFlowSettings settings)
	{
		return UnitTestsRunner.RunTest(testItem, sqlConnectionString, settings);
	}

	public void PrepareUnitTestingDatabase(string sqlConnectionString)
	{
		// TODO: drop database if exists and recreate it.

		//if (!DbExists(SqlConnectionString))
		//{			
		//}

		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false /* we will run tests in Visual Studio UI */
		};

		EasyFlow.DeployProject(_projectPath, sqlConnectionString, deployParams);
	}

}