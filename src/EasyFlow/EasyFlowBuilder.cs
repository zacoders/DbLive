namespace EasyFlow;

public static class EasyFlowBuilderExtentions
{
	public static EasyFlowBuilder LogToConsole(this EasyFlowBuilder builder)
	{
		var logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		builder.Container.AddSingleton<ILogger>(logger);

		return builder;
	}

	public static EasyFlowBuilder DbConnection(this EasyFlowBuilder builder, string sqlDbConnectionString)
	{
		var cnn = new EasyFlowDbConnection(sqlDbConnectionString);
		builder.Container.AddSingleton<IEasyFlowDbConnection>(cnn);
		return builder;
	}

	public static EasyFlowBuilder Project(this EasyFlowBuilder builder, string projectPath)
	{
		var cnn = new EasyFlowProjectPath(projectPath);		
		builder.Container.AddSingleton<IEasyFlowProjectPath>(cnn);
		return builder;
	}

	public static IEasyFlow CreateDeployer(this EasyFlowBuilder builder)
	{
		builder.Container.InitializeEasyFlow();
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlow>()!;
	}
}
