using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Common;

public static class Bootstrapper
{
	public static void InitializeCommon(this IServiceCollection container)
	{
		container.AddSingleton<ITimeProvider, TimeProvider>();
		container.AddSingleton<IFileSystem, FileSystem>();
	}
}
