using EasyFlow.Deployers.Migrations;
using EasyFlow.Deployers.Tests;
using EasyFlow.Testing;

namespace EasyFlow;

public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container)
	{
		container.AddSingleton<ITimeProvider, Common.TimeProvider>();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IEasyFlowProject, EasyFlowProject>();
		container.AddSingleton<ISettingsAccessor, SettingsAccessor>();
		container.AddSingleton<BreakingChangesDeployer>();
		container.AddSingleton<CodeDeployer>();
		container.AddSingleton<MigrationsDeployer>();
		container.AddSingleton<MigrationItemDeployer>();
		container.AddSingleton<FolderDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IEasyFlowTester, EasyFlowTester>();
		container.AddSingleton<IEasyFlow, EasyFlow>();
	}
}
