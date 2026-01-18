namespace DbLive.MSSQL;

public static class DbLiveBuilderExtensions
{
	public static DbLiveBuilder SqlServer(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			services.InitializeMSSQL();
		});
	}
}
