
using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class Bootstrapper
{
	public static void InitializeDbLive(this IServiceCollection container)
	{
		container.AddSingleton<ITimeProvider, Common.TimeProvider>();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IDbLiveProject, DbLiveProject>();
		container.AddSingleton<IInternalDbLiveProject, InternalDbLiveProject>();
		container.AddSingleton<ISettingsAccessor, SettingsAccessor>();
		container.AddSingleton<IVsProjectPathAccessor, ProjectPathAccessor>();
		container.AddSingleton<IBreakingChangesDeployer, BreakingChangesDeployer>();
		container.AddSingleton<ICodeItemDeployer, CodeItemDeployer>();
		container.AddSingleton<ICodeDeployer, CodeDeployer>();
		container.AddSingleton<IMigrationsDeployer, MigrationsDeployer>();
		container.AddSingleton<IMigrationVersionDeployer, MigrationVersionDeployer>();
		container.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>();
		container.AddSingleton<IFolderDeployer, FolderDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IUnitTestItemRunner, UnitTestItemRunner>();
		container.AddSingleton<IDbLiveTester, DbLiveTester>();
		container.AddSingleton<IDbLive, DbLive>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
		container.AddSingleton<IDbLiveDeployer, DbLiveDeployer>();
		container.AddSingleton<IDbLiveSelfDeployer, DbLiveSelfDeployer>();
		container.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>();
	}
}
