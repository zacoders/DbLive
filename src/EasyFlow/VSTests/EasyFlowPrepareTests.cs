namespace EasyFlow.VSTests;

public class EasyFlowPrepareTests(
	IEasyFlow _easyFlow, 
	IEasyFlowProject _easyFlowProject, 
	IUnitTestsRunner _unitTestsRunner
) : IEasyFlowPrepareTests
{
	private static IServiceCollection Container = new ServiceCollection();
	private string? _projectPath;
		
	public TestItem[] TestItems { get; private set; } = [];

	public TestRunResult Run(TestItem testItem, string sqlConnectionString, EasyFlowSettings settings)
	{
		return _unitTestsRunner.RunTest(testItem, sqlConnectionString, settings);
	}

	public void Load(string projectPath)
	{
		_projectPath = projectPath;
		_easyFlowProject.Load(projectPath);
		TestItems = [.. _easyFlowProject.GetTests()];
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

		_easyFlow.DeployProject(
			_projectPath ?? throw new Exception("Please call 'Load()' first."), 
			sqlConnectionString, 
			deployParams
		);
	}
}
