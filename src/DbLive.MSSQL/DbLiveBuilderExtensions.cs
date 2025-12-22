using DbLive.Common;

namespace DbLive.MSSQL;

public static class DbLiveBuilderExtensions
{
	public static IDbLiveBuilder SqlServer(this IDbLiveBuilder builder)
	{
		builder.Container.InitializeMSSQL();

		return builder;
	}
}
