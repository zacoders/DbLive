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

	/// <summary>The first column of the single row of the query, as a number: a count, an ID.</summary>
	public static async Task<long> ScalarAsync(this IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.QuerySqlAsync(query).ConfigureAwait(false);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}

	/// <summary>The first column of every row of the query as text, sorted ordinally.</summary>
	public static async Task<List<string>> ListAsync(this IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.QuerySqlAsync(query).ConfigureAwait(false);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
