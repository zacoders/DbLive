namespace EasyFlow.VSTests;

public class PrepareTests
{
	private IServiceCollection Container { get; }
	public IReadOnlyCollection<TestItem> TestItems { get; }

	public PrepareTests(DBEngine dBEngine, string projectPath)
	{
		Container = new ServiceCollection();
		Container.InitializeEasyFlow(dBEngine);
		var serviceProvider = Container.BuildServiceProvider();
		var project = serviceProvider.GetService<IEasyFlowProject>()!;
		project.Load(projectPath);
		TestItems = project.GetTests();
	}
}