using DbLive.Adapter;
using DbLive.Deployers;

namespace DbLive.PostgreSQL;

public static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, PostgreSqlDA>()
			.AddSingleton<IDeployLock, PostgreSqlDeployLock>()
			.AddSingleton<IInternalProjectPath, PostgreSqlProjectPath>();
	}
}
