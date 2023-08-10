namespace EasyFlow;

public static class Bootstrapper
{
	public static void InitializeEasyFlow(this IServiceCollection container)
	{
		container.InitializeMSSQL();
		container.InitializeFlowProject();
		container.AddSingleton<EasyFlowDeploy>();
	}
}
