using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Adapter.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, PostgreSqlDA>();
		container.AddSingleton<IEasyFlowDeployer, PostgreSqlDeployer>();
	}
}
