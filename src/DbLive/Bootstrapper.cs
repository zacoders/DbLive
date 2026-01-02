
using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class Bootstrapper
{
	public static void InitializeDbLive(this IServiceCollection container)
	{
		// ordered alphabetically
		container.AddSingleton<IBreakingChangesDeployer, BreakingChangesDeployer>();
		container.AddSingleton<ICodeDeployer, CodeDeployer>();
		container.AddSingleton<ICodeItemDeployer, CodeItemDeployer>();
		container.AddSingleton<IDbLive, DbLive>();
		container.AddSingleton<IDbLiveDeployer, DbLiveDeployer>();
		container.AddSingleton<IDbLiveProject, DbLiveProject>();
		container.AddSingleton<IDbLiveSelfDeployer, DbLiveSelfDeployer>();
		container.AddSingleton<IDbLiveTester, DbLiveTester>();
		container.AddSingleton<IDowngradeDeployer, DowngradeDeployer>();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IFolderDeployer, FolderDeployer>();
		container.AddSingleton<IInternalDbLiveProject, InternalDbLiveProject>();
		container.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>();
		container.AddSingleton<IMigrationsDeployer, MigrationsDeployer>();
		container.AddSingleton<IMigrationsSaver, MigrationsSaver>();
		container.AddSingleton<IMigrationVersionDeployer, MigrationVersionDeployer>();
		container.AddSingleton<ISettingsAccessor, SettingsAccessor>();
		container.AddSingleton<ITimeProvider, Common.TimeProvider>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
		container.AddSingleton<IUnitTestItemRunner, UnitTestItemRunner>();
		container.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IVsProjectPathAccessor, ProjectPathAccessor>();
	}
}
