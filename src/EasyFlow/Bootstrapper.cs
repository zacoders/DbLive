namespace EasyFlow;

public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container, DBEngine dbEngine)
	{
		container.InitializeFlowProject();
		container.AddSingleton<IEasyFlowDeploy, EasyFlowDeploy>();

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
