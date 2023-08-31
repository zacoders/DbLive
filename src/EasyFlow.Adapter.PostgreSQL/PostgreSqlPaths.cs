namespace EasyFlow.Adapter.PostgreSQL;

internal class PostgreSqlPaths : IEasyFlowPaths
{
	public string GetPathToEasyFlowSelfProject() =>
		Path.Combine(AppContext.BaseDirectory, @"EasySqlFlow\PostgreSQL");
}