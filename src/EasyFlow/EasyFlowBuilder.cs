namespace EasyFlow;

public static class EasyFlowBuilderExtentions
{
	public static EasyFlowBuilder LogToConsole(this EasyFlowBuilder builder)
	{
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

	public static EasyFlowBuilder SetDbConnection(this EasyFlowBuilder builder, string sqlDbConnectionString)
	{
		builder.Container.SetDbConnection(sqlDbConnectionString);
		return builder;
	}

	public static void SetDbConnection(this IServiceCollection serviceCollection, string sqlDbConnectionString)
	{
		var cnn = new EasyFlowDbConnection(sqlDbConnectionString);
		serviceCollection.AddSingleton<IEasyFlowDbConnection>(cnn);
	}

	public static EasyFlowBuilder SetProjectPath(this EasyFlowBuilder builder, string projectPath)
	{
		builder.Container.SetProjectPath(projectPath);
		return builder;
	}

	public static void SetProjectPath(this IServiceCollection serviceCollection, string projectPath)
	{
		var path = new EasyFlowProjectPath(projectPath);
		serviceCollection.AddSingleton<IEasyFlowProjectPath>(path);
	}

	public static IEasyFlow CreateDeployer(this EasyFlowBuilder builder)
	{
		builder.Container.InitializeEasyFlow();
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlow>()!;
	}
}
