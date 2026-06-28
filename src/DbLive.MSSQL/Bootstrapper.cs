using DbLive.Adapter;
using DbLive.Deployers;

namespace DbLive.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, MsSqlDA>()
			.AddSingleton<IDeployLock, MsSqlDeployLock>()
			.AddSingleton<IInternalProjectPath, MsSqlProjectPath>();
	}
}
