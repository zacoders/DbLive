using EasyFlow.Adapter;
using EasyFlow.Testing;

namespace EasyFlow;

[ExcludeFromCodeCoverage]
public static class EasyFlowBuilderExtentions
{
	public static IEasyFlowBuilder LogToConsole(this IEasyFlowBuilder builder)
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

	public static IEasyFlowBuilder SetDbConnection(this IEasyFlowBuilder builder, string sqlDbConnectionString)
	{
		builder.Container.SetDbConnection(sqlDbConnectionString);
		return builder;
	}

	public static void SetDbConnection(this IServiceCollection serviceCollection, string sqlDbConnectionString)
	{
		var cnn = new EasyFlowDbConnection(sqlDbConnectionString);
		serviceCollection.AddSingleton<IEasyFlowDbConnection>(cnn);
	}

	public static IEasyFlowBuilder SetProjectPath(this IEasyFlowBuilder builder, string projectPath)
	{
		builder.Container.SetProjectPath(projectPath);
		return builder;
	}

	public static void SetProjectPath(this IServiceCollection serviceCollection, string projectPath)
	{
		var path = new ProjectPath(projectPath);
		serviceCollection.AddSingleton<IProjectPath>(path);
	}

	public static IEasyFlow CreateDeployer(this IEasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlow>()!;
	}

	public static IEasyFlowInternal CreateSelfDeployer(this IEasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowInternal>()!;
	}

	internal static IEasyFlowDA CreateEasyFlowDA(this IEasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowDA>()!;
	}

	public static IEasyFlowTester CreateTester(this IEasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowTester>()!;
	}

	public static IEasyFlowProject CreateProject(this IEasyFlowBuilder builder)
	{
		var serviceProvider = builder.Container.BuildServiceProvider();
		return serviceProvider.GetService<IEasyFlowProject>()!;
	}
}
