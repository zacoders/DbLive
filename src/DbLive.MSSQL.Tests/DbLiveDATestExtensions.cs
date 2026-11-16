using DbLive.Adapter;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// The SQL of the tests themselves, through the data access of DbLive: arranging data, reading what a deployment left. The data access
/// takes no default isolation level or timeout; these are the ones of the tests.
/// </summary>
internal static class DbLiveDATestExtensions
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

	public static Task ExecuteSqlAsync(this IDbLiveDA da, string sql) =>
		da.ExecuteNonQueryAsync(sql, TranIsolationLevel.ReadCommitted, Timeout);

	public static Task<List<SqlResult>> QuerySqlAsync(this IDbLiveDA da, string sql) =>
		da.ExecuteQueryMultipleAsync(sql, TranIsolationLevel.ReadCommitted, Timeout);
}
