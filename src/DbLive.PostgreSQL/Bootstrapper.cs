using DbLive.Adapter;

namespace DbLive.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, PostgreSqlDA>()
			.AddSingleton<IInternalProjectPath, PostgreSqlProjectPath>();
	}
}
