using DbLive.Adapter;
using DbLive.SelfDeployer;

namespace DbLive.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, MsSqlDA>()
			.AddSingleton<IInternalScriptSource>(new InternalScriptSource(typeof(MsSqlDA).Assembly));
	}
}
