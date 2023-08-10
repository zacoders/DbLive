using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Adapter.MSSQL;

public static class Bootstrapper
{
	public static void InitializeMSSQL(this IServiceCollection container)
	{
		container.AddSingleton<IEasyFlowDA, EasyFlowDA>();
		container.AddSingleton<IEasyFlowDeployer, MsSqlDeployer>();
	}
}
