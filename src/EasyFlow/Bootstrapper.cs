using EasyFlow.VSTests;

namespace EasyFlow;

public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container)
	{
		container.InitializeCommon();
		container.InitializeFlowProject();
		container.AddSingleton<BreakingChangesDeployer>();
		container.AddSingleton<CodeDeployer>();
		container.AddSingleton<MigrationsDeployer>();
		container.AddSingleton<MigrationItemDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IEasyFlow, EasyFlow>();
		container.AddSingleton<IEasyFlowPrepareTests, EasyFlowPrepareTests>();
	}
}
