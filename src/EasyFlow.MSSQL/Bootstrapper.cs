using EasyFlow.Adapter;

namespace EasyFlow.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, MsSqlDA>();
		container.AddSingleton<IEasyFlowPaths, MsSqlPaths>();
	}
}
