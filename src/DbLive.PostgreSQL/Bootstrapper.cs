using DbLive.Adapter;
using DbLive.SelfDeployer;

namespace DbLive.PostgreSQL;

internal static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveDA, PostgreSqlDA>()
			.AddSingleton<IInternalScriptSource>(new InternalScriptSource(typeof(PostgreSqlDA).Assembly));
	}
}
