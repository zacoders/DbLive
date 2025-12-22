using DbLive.Adapter;

namespace DbLive.PostgreSQL;

internal class PostgreSqlPaths : IDbLivePaths
{
	public string GetPathToDbLiveSelfProject() =>
		Path.Combine(
			AppContext.BaseDirectory,
			GetType().Assembly.GetName().Name ?? throw new Exception("Unknown assembly name.")
		);
}