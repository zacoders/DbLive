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

	public static IEasyFlow CreateDeployer(this EasyFlowBuilder builder)
	{
		builder.Container.InitializeEasyFlow();

		var serviceProvider = builder.Container.BuildServiceProvider();

		return serviceProvider.GetService<IEasyFlow>()!;
	}
}
