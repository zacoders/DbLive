namespace EasyFlow.Project;

public static class Bootstrapper
{
	public static void InitializeFlowProject(this IServiceCollection container)
	{
		container.InitializeCommon();
		container.AddSingleton<IEasyFlowProject, EasyFlowProject>();
	}
}
