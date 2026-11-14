using DbLive.Adapter;
using DbLive.SelfDeployer;

namespace DbLive.PostgreSQL;

internal static class Bootstrapper
{
	public static void InitializePostgreSQL(this IServiceCollection container)
	{
		_ = container
			.AddSingleton<IDbLiveInternalDA, PostgreSqlDA>()
			// the public part of the same instance: DbLiveBuilder.CreateDbLiveDA().
			.AddSingleton<IDbLiveDA>(services => services.GetRequiredService<IDbLiveInternalDA>())
			.AddSingleton<IInternalScriptSource>(new InternalScriptSource(typeof(PostgreSqlDA).Assembly));
	}
}
