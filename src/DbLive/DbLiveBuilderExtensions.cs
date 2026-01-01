
namespace DbLive;

[ExcludeFromCodeCoverage]
public static class DbLiveBuilderExtensions
{
	public static IDbLiveBuilder WithNoLogs(this IDbLiveBuilder builder)
	{
		var logger = Serilog.Core.Logger.None;
		builder.Container.AddSingleton<ILogger>(logger);
		return builder;
	}

	public static IDbLiveBuilder LogToConsole(this IDbLiveBuilder builder)
	{
		// todo: if LogToConsole() is not called builder is failing.
		builder.Container.LogToConsole();
		return builder;
	}

	public static void LogToConsole(this IServiceCollection serviceCollection)
	{
		var logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		serviceCollection.AddSingleton<ILogger>(logger);
	}

	public static IDbLiveBuilder SetDbConnection(this IDbLiveBuilder builder, string sqlDbConnectionString)
	{
		builder.Container.SetDbConnection(sqlDbConnectionString);
		return builder;
	}

	public static void SetDbConnection(this IServiceCollection serviceCollection, string sqlDbConnectionString)
	{
		var cnn = new DbLiveDbConnection(sqlDbConnectionString);
		serviceCollection.AddSingleton<IDbLiveDbConnection>(cnn);
	}

	public static IDbLiveBuilder SetProjectPath(this IDbLiveBuilder builder, string projectPath)
	{
		builder.Container.SetProjectPath(projectPath);
		return builder;
	}

	public static void SetProjectPath(this IServiceCollection serviceCollection, string projectPath)
	{
		var path = new ProjectPath(projectPath);
		serviceCollection.AddSingleton<IProjectPath>(path);
	}

	public static IDbLive CreateDeployer(this IDbLiveBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLive>()!;
	}

	internal static IDbLiveDA CreateDbLiveDA(this IDbLiveBuilder builder)
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
