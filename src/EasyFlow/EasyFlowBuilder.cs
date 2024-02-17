using EasyFlow.Adapter;
using EasyFlow.Testing;

namespace EasyFlow;

[ExcludeFromCodeCoverage]
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
		var path = new ProjectPath(projectPath);
		serviceCollection.AddSingleton<IProjectPath>(path);
	}

	public static IEasyFlow CreateDeployer(this EasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlow>()!;
	}

	internal static IEasyFlowDA CreateEasyFlowDA(this EasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowDA>()!;
	}

	public static IEasyFlowTester CreateTester(this EasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowTester>()!;
	}

	public static IEasyFlowProject CreateProject(this EasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowProject>()!;
	}
}
