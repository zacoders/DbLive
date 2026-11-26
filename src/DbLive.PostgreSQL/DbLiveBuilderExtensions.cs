namespace DbLive.PostgreSQL;

/// <summary>
/// Configures a <see cref="DbLiveBuilder"/> for PostgreSQL.
/// </summary>
public static class DbLiveBuilderExtensions
{
	/// <summary>
	/// Uses the PostgreSQL provider: each script runs as one command.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <returns>The same builder instance for chaining.</returns>
	public static DbLiveBuilder PostgreSQL(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			services.InitializePostgreSQL();
		});
	}
}
