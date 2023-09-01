namespace EasyFlow.Adapter.PostgreSQL;

internal class PostgreSqlPaths : IEasyFlowPaths
{
	public string GetPathToEasyFlowSelfProject() =>
		Path.Combine(
			AppContext.BaseDirectory, 
			GetType().Assembly.GetName().Name ?? throw new Exception("Unknow assembly name.")
		);
}