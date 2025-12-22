using DbLive.Adapter;

namespace DbLive.MSSQL;

internal class MsSqlPaths : IDbLivePaths
{
	public string GetPathToDbLiveSelfProject() =>
		Path.Combine(
			AppContext.BaseDirectory,
			GetType().Assembly.GetName().Name ?? throw new Exception("Unknown assembly name.")
		);
}