namespace EasyFlow.Adapter.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, MsSqlDA>();
		container.AddSingleton<IEasyFlowDeployer, MsSqlDeployer>();
		container.AddSingleton<IEasyFlowPaths, MsSqlPaths>();
	}
}
