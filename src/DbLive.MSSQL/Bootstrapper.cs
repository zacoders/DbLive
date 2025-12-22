using DbLive.Adapter;

namespace DbLive.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		container.AddSingleton<IDbLiveDA, MsSqlDA>();
		container.AddSingleton<IDbLivePaths, MsSqlPaths>();
	}
}
