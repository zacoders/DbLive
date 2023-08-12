using EasyFlow.Adapter.PostgreSQL;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Adapter.MSSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, PostgreSqlDA>();
		container.AddSingleton<IEasyFlowDeployer, PostgreSqlDeployer>();
	}
}
