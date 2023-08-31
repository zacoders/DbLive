using EasyFlow.Adapter.MSSQL;

namespace EasyFlow.Adapter.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, PostgreSqlDA>();
		container.AddSingleton<IEasyFlowDeployer, PostgreSqlDeployer>();
        container.AddSingleton<IEasyFlowPaths, PostgreSqlPaths>();
    }
}
