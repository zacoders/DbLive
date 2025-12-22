using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using DbLive.Deployers.Testing;
using DbLive.Testing;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class Bootstrapper
{
	public static void InitializeDbLive(this IServiceCollection container)
	{
		container.AddSingleton<ITimeProvider, Common.TimeProvider>();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IDbLiveProject, DbLiveProject>();
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
		container.AddSingleton<IDbLiveTester, DbLiveTester>();
		container.AddSingleton<IDbLive, DbLive>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
		container.AddSingleton<IDbLiveInternal, DbLiveInternal>();
		container.AddSingleton<IDbLiveInternalManager, DbLiveInternalManager>();
		container.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>();
	}
}
