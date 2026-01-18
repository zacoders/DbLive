using Serilog.Core;
using System.Reflection;

namespace DbLive;

[ExcludeFromCodeCoverage]
public static class DbLiveBuilderExtensions
{
	public static DbLiveBuilder LogToConsole(this DbLiveBuilder builder)
	{
		return builder.ConfigureServices(services =>
		{
			Logger logger = new LoggerConfiguration()
				.WriteTo.Console()
				.CreateLogger();

			_ = services.AddSingleton<ILogger>(logger);
		});
	}

	public static DbLiveBuilder SetDbConnection(this DbLiveBuilder builder, string sqlDbConnectionString)
	{
		return builder.ConfigureServices(services =>
		{
			_ = services.AddSingleton<IDbLiveDbConnection>(new DbLiveDbConnection(sqlDbConnectionString));
		});
	}

	//public static DbLiveBuilder SetProjectPath(this DbLiveBuilder builder, string projectPath)
	//{
	//	return builder.ConfigureServices(services =>
	//	{
	//		_ = services.AddSingleton<IProjectPath>(new ProjectPath(projectPath));
	//	});
	//}

	public static DbLiveBuilder SetProject(this DbLiveBuilder builder, Assembly projectAssembly)
	{
		string outputPath = Path.GetFullPath(projectAssembly.GetName().Name!);
		string vsProjectPath = Path.Combine(GetVisualStudioProjectPath(projectAssembly), "Scripts");

		return builder.ConfigureServices(services =>
		{
			ProjectPath projectPath = new(outputPath, vsProjectPath);
			_ = services.AddSingleton<IProjectPath>(projectPath);
		});
	}

	private static string GetVisualStudioProjectPath(Assembly projectAssembly)
	{
		var attr = projectAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
						   .FirstOrDefault(a => a.Key == "ProjectDir");

		return attr?.Value ?? throw new Exception("ProjectDir metadata not found!");
	}
}

