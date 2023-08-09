using EasySqlFlow.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace EasySqlFlow;

public static class Bootstrapper
{
	public static void InitializeEasySqlFlow(this IServiceCollection container)
	{
		container.InitializeDataAccess();
		container.AddSingleton<IFileSystem, FileSystem>();
		container.AddSingleton<DeploySQL>();
	}
}
