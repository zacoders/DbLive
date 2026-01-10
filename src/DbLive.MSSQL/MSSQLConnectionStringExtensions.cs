namespace DbLive.MSSQL;

public static class MSSQLConnectionStringExtensions
{
	public static string SetMsSqlDatabaseName(this string mssqlConnectionString, string dbName)
	{
		SqlConnectionStringBuilder cnnBuilder = new(mssqlConnectionString)
		{
			InitialCatalog = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	public static string SetRandomMsSqlDatabaseName(this string mssqlConnectionString)
	{
		return mssqlConnectionString.SetMsSqlDatabaseName($"DbLive--{Guid.NewGuid()}");
	}
}
