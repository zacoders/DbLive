using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using DbLive.Deployers.Testing;
using DbLive.Testing;

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
		container.AddSingleton<IBreakingChangesDeployer, BreakingChangesDeployer>();
		container.AddSingleton<ICodeItemDeployer, CodeItemDeployer>();
		container.AddSingleton<ICodeDeployer, CodeDeployer>();
		container.AddSingleton<IMigrationsDeployer, MigrationsDeployer>();
		container.AddSingleton<IMigrationDeployer, MigrationDeployer>();
		container.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>();
		container.AddSingleton<IFolderDeployer, FolderDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IUnitTestItemRunner, UnitTestItemRunner>();
		container.AddSingleton<IEasyFlowTester, EasyFlowTester>();
		container.AddSingleton<IEasyFlow, EasyFlow>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
		container.AddSingleton<IEasyFlowInternal, EasyFlowInternal>();
		container.AddSingleton<IEasyFlowInternalManager, EasyFlowInternalManager>();
		container.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>();
	}
}
