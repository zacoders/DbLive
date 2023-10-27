namespace EasyFlow;

public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container, DBEngine dbEngine)
	{
		container.InitializeFlowProject();
		container.AddSingleton<BreakingChangesDeployer>();
		container.AddSingleton<CodeDeployer>();
		container.AddSingleton<MigrationsDeployer>();
		container.AddSingleton<MigrationItemDeployer>();
		container.AddSingleton<UnitTestsRunner>();
		container.AddSingleton<IEasyFlow, EasyFlow>();

		switch (dbEngine)
		{
			case DBEngine.MSSQL:
				container.InitializeMSSQL();
				break;
			case DBEngine.PostgreSql:
				container.InitializePostgreSQL();
				break;
			default:
				throw new NotImplementedException($"Unsupported DBEngine '{dbEngine}' provided.");
		}
	}
}
