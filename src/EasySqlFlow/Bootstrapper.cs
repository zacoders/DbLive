namespace EasySqlFlow;

public static class Bootstrapper
{
	public static void InitializeEasySqlFlow(this IServiceCollection container)
	{
		container.InitializeDataAccess();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<ISqlProject, SqlProject>();
		container.AddSingleton<DeploySQL>();
	}
}
