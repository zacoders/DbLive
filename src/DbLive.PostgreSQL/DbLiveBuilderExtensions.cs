using DbLive.Common;

namespace DbLive.PostgreSQL;

public static class DbLiveBuilderExtensions
{
	public static DbLiveBuilder PostgreSQL(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			services.InitializePostgreSQL();
		});
	}
}
