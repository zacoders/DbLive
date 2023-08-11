namespace EasyFlow.Adapter.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		container.AddSingleton<MsSqlDA>();
		container.AddSingleton<MsSqlDeployer>();
	}
}
