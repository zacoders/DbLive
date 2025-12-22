namespace EasyFlow.MSSQL;

public static class StringExtentions
{
	public static string SetDatabaseName(this string mssqlConnectionString, string dbName)
	{
		SqlConnectionStringBuilder cnnBuilder = new(mssqlConnectionString)
		{
			InitialCatalog = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	public static string SetRandomDatabaseName(this string mssqlConnectionString)
	{
		return mssqlConnectionString.SetDatabaseName($"EasyFlow--{Guid.NewGuid()}");
	}
}
