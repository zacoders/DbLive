using DbLive.Adapter;

namespace EasyFlow.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, PostgreSqlDA>();
		container.AddSingleton<IEasyFlowPaths, PostgreSqlPaths>();
	}
}
