namespace DbLive.Common;

public class DbLiveDbConnection(string connectionString) : IDbLiveDbConnection
{
	public string ConnectionString { get; } = connectionString;
}
