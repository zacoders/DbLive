namespace EasySqlFlow.DataAccess;

public static class Bootstrapper
{
	public static void InitializeDataAccess(this IServiceCollection container)
	{
		container.AddSingleton<IEasySqlFlowDA, EasySqlFlowDA>();
	}
}
