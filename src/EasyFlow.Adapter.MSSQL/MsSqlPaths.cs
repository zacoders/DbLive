namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlPaths : IEasyFlowPaths
{
	public string GetPathToEasyFlowSelfProject() =>
		Path.Combine(
			AppContext.BaseDirectory,
			GetType().Assembly.GetName().Name ?? throw new Exception("Unknow assembly name.")
		);
}