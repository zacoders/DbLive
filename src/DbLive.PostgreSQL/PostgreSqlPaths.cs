using DbLive.Adapter;

namespace EasyFlow.PostgreSQL;

internal class PostgreSqlPaths : IEasyFlowPaths
{
	public string GetPathToEasyFlowSelfProject() =>
		Path.Combine(
			AppContext.BaseDirectory,
			GetType().Assembly.GetName().Name ?? throw new Exception("Unknown assembly name.")
		);
}