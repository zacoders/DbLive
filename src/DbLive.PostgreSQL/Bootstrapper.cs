using DbLive.Adapter;

namespace DbLive.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		container.AddSingleton<IDbLiveDA, PostgreSqlDA>();
		container.AddSingleton<IDbLivePaths, PostgreSqlPaths>();
	}
}
