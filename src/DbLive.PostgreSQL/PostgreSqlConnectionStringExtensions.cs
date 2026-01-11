
namespace DbLive.PostgreSQL;

public static class PostgreSqlConnectionStringExtensions
{
	public static string SetPostgreSqlDatabaseName(this string pgConnectionString, string dbName)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(pgConnectionString)
		{
			Database = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	public static string SetRandomPostgreSqlDatabaseName(this string pgConnectionString)
	{
		return pgConnectionString.SetPostgreSqlDatabaseName($"dblive--{Guid.NewGuid()}");
	}
}