
namespace DbLive;

[ExcludeFromCodeCoverage]
public static class DbLiveBuilderExtensions
{
	//public static IDbLiveBuilder WithNoLogs(this IDbLiveBuilder builder)
	//{
	//	var logger = Serilog.Core.Logger.None;
	//	builder.Container.AddSingleton<ILogger>(logger);
	//	return builder;
	//}

	public static IDbLiveBuilder LogToConsole(this IDbLiveBuilder builder)
	{
		var logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		builder.Container.AddSingleton<ILogger>(logger);

		return builder;
	}

	public static IDbLiveBuilder SetDbConnection(this IDbLiveBuilder builder, string sqlDbConnectionString)
	{
		var cnn = new DbLiveDbConnection(sqlDbConnectionString);
		builder.Container.AddSingleton<IDbLiveDbConnection>(cnn);
		return builder;
	}


	public static IDbLiveBuilder SetProjectPath(this IDbLiveBuilder builder, string projectPath)
	{
		var path = new ProjectPath(projectPath);
		builder.Container.AddSingleton<IProjectPath>(path);
		return builder;
	}

	public static IDbLive CreateDeployer(this IDbLiveBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLive>()!;
	}

	public static IDbLiveDA CreateDbLiveDA(this IDbLiveBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLiveDA>()!;
	}

	public static IDbLiveTester CreateTester(this IDbLiveBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLiveTester>()!;
	}

	public static IDbLiveProject CreateProject(this IDbLiveBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLiveProject>()!;
	}
}
