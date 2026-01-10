using DbLive.Adapter;
namespace DbLive.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, MsSqlDA>()
			.AddSingleton<IInternalProjectPath, MsSqlProjectPath>();
	}
}
