
using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class Bootstrapper
{
	public static void InitializeDbLive(this IServiceCollection container)
	{
		// ordered alphabetically
		_ = container
			.AddSingleton<IBreakingChangesDeployer, BreakingChangesDeployer>()
			.AddSingleton<ICodeDeployer, CodeDeployer>()
			.AddSingleton<ICodeItemDeployer, CodeItemDeployer>()
			.AddSingleton<IDbLive, DbLive>()
			.AddSingleton<IDbLiveDeployer, DbLiveDeployer>()
			.AddSingleton<IDbLiveProject, DbLiveProject>()
			.AddSingleton<IDbLiveSelfDeployer, DbLiveSelfDeployer>()
			.AddSingleton<IDbLiveTester, DbLiveTester>()
			.AddSingleton<IDowngradeDeployer, DowngradeDeployer>()
			.AddSingleton<IFileSystem, FileSystem>()
			.AddSingleton<IFolderDeployer, FolderDeployer>()
			.AddSingleton<IInternalDbLiveProject, InternalDbLiveProject>()
			.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>()
			.AddSingleton<IMigrationsDeployer, MigrationsDeployer>()
			.AddSingleton<IMigrationsSaver, MigrationsSaver>()
			.AddSingleton<IMigrationVersionDeployer, MigrationVersionDeployer>()
			.AddSingleton<ISettingsAccessor, SettingsAccessor>()
			.AddSingleton<ITimeProvider, Common.TimeProvider>()
			.AddSingleton<ITransactionRunner, TransactionRunner>()
			.AddSingleton<IUnitTestItemRunner, UnitTestItemRunner>()
			.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>()
			.AddSingleton<IUnitTestsRunner, UnitTestsRunner>()
			.AddSingleton<IVsProjectPathAccessor, ProjectPathAccessor>();
	}
}
