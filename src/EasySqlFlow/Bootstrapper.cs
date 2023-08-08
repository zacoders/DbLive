using Microsoft.Extensions.DependencyInjection;

public static class Bootstrapper
{
	public static void Initialize(IServiceCollection container)
	{
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<DeploySQL>();
	}
}
