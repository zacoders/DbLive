namespace DbLive.MSSQL;

/// <summary>
/// Configures a <see cref="DbLiveBuilder"/> for SQL Server.
/// </summary>
public static class DbLiveBuilderExtensions
{
	/// <summary>
	/// Uses the SQL Server provider: the scripts are T-SQL, with <c>GO</c> batches.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <returns>The same builder instance for chaining.</returns>
	public static DbLiveBuilder SqlServer(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			services.InitializeMSSQL();
		});
	}
}
