namespace DbLive.Common;

internal class DbLiveDbConnection(string connectionString) : IDbLiveDbConnection
{
	public string ConnectionString { get; } = connectionString;
}
