namespace EasyFlow.Project;

public static class Bootstrapper
{
	public static void InitializeFlowProject(this IServiceCollection container)
	{
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<IEasyFlowProject, EasyFlowProject>();
	}
}
