namespace EasyFlow.VSTests;

public class EasyFlowPrepareTests
{
	private static IServiceCollection Container = new ServiceCollection();

	private IUnitTestsRunner unitTestsRunner { get; init; }

	public TestItem[] TestItems { get; private set; } = [];

	public EasyFlowPrepareTests (DBEngine dBEngine, string projectPath)
	{
		Container.InitializeEasyFlow(dBEngine);
		var serviceProvider = Container.BuildServiceProvider();

		unitTestsRunner = serviceProvider.GetService<IUnitTestsRunner>()!;
		
		var project = serviceProvider.GetService<IEasyFlowProject>()!;
		project.Load(projectPath);
		TestItems = [.. project.GetTests()];
	}

	public TestRunResult Run(TestItem testItem, string sqlConnectionString, EasyFlowSettings settings)
	{
		return unitTestsRunner.RunTest(testItem, sqlConnectionString, settings);
	}
}