
using Serilog.Core;

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

			services.AddSingleton<ILogger>(logger);
		});
	}

	public static DbLiveBuilder SetDbConnection(this DbLiveBuilder builder, string sqlDbConnectionString)
	{
		return builder.ConfigureServices(services =>
		{
			services.AddSingleton<IDbLiveDbConnection>(new DbLiveDbConnection(sqlDbConnectionString));
		});
	}

	public static DbLiveBuilder SetProjectPath(this DbLiveBuilder builder, string projectPath)
	{
		return builder.ConfigureServices(services =>
		{
			services.AddSingleton<IProjectPath>(new ProjectPath(projectPath));
		});
	}
}

