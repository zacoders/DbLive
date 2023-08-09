using EasySqlFlow.DataAccess;
using Microsoft.Extensions.DependencyInjection;

public static class Bootstrapper
{
	public static void InitializeDataAccess(this IServiceCollection container)
	{
		container.AddSingleton<IEasySqlFlowDA, EasySqlFlowDA>();
	}
}
