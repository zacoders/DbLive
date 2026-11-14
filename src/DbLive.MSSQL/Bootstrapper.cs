using DbLive.Adapter;
using DbLive.SelfDeployer;

namespace DbLive.MSSQL;

internal static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveInternalDA, MsSqlDA>()
			// the public part of the same instance: DbLiveBuilder.CreateDbLiveDA().
			.AddSingleton<IDbLiveDA>(services => services.GetRequiredService<IDbLiveInternalDA>())
			.AddSingleton<IInternalScriptSource>(new InternalScriptSource(typeof(MsSqlDA).Assembly));
	}
}
