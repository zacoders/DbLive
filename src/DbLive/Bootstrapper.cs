
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
		container.AddSingleton<IMigrationVersionDeployer, MigrationVersionDeployer>();
		container.AddSingleton<IMigrationItemDeployer, MigrationItemDeployer>();
		container.AddSingleton<IFolderDeployer, FolderDeployer>();
		container.AddSingleton<IUnitTestsRunner, UnitTestsRunner>();
		container.AddSingleton<IUnitTestItemRunner, UnitTestItemRunner>();
		container.AddSingleton<IDbLiveTester, DbLiveTester>();
		container.AddSingleton<IDbLive, DbLive>();
		container.AddSingleton<ITransactionRunner, TransactionRunner>();
		container.AddSingleton<IDbLiveInternalDeployer, DbLiveInternalDeployer>();
		container.AddSingleton<IDbLiveSelfDeployer, DbLiveSelfDeployer>();
		container.AddSingleton<IUnitTestResultChecker, UnitTestResultChecker>();
	}
}
