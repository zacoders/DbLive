using EasyFlow.Deployers.Code;
using EasyFlow.Deployers.Migrations;
using EasyFlow.Deployers.Testing;
using EasyFlow.Testing;

namespace EasyFlow;

[ExcludeFromCodeCoverage]
public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container)
	{
		container.AddSingleton<ITimeProvider, Common.TimeProvider>();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IEasyFlowProject, EasyFlowProject>();
		container.AddSingleton<ISettingsAccessor, SettingsAccessor>();
		container.AddSingleton<IProjectPathAccessor, ProjectPathAccessor>();
		container.AddSingleton<BreakingChangesDeployer>();
		container.AddSingleton<ICodeItemDeployer, CodeItemDeployer>();
		container.AddSingleton<CodeDeployer>();
		container.AddSingleton<MigrationsDeployer>();
		container.AddSingleton<IMigrationDeployer, MigrationDeployer>();
		container.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>();
		container.AddSingleton<FolderDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IEasyFlowTester, EasyFlowTester>();
		container.AddSingleton<IEasyFlow, EasyFlow>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
	}
}
