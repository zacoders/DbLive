
namespace EasyFlow.Tests.Common;


public static class EasyFlowBuilderExtensions
{
	public static void AddXUnitLogger(this IServiceCollection serviceCollection, ITestOutputHelper output)
	{
		var logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();

		serviceCollection.AddSingleton<ILogger>(logger);
	}

	//public static EasyFlowBuilder AddTestingMsSqlConnection(this EasyFlowBuilder builder)
	//{
	//	builder.Container.AddTestingMsSqlConnection();
	//	return builder;
	//}

	//public static void AddTestingMsSqlConnection(this IServiceCollection serviceCollection)
	//{
	//	var testConfig = new TestConfig();
	//	var cnn = new EasyFlowDbConnection(testConfig.GetSqlServerConnectionString());
	//	serviceCollection.AddSingleton<IEasyFlowDbConnection>(cnn);
	//}

	//public static void AddTestingPostgreSQLConnection(this IServiceCollection serviceCollection)
	//{
	//	var testConfig = new TestConfig();
	//	var cnn = new EasyFlowDbConnection(testConfig.GetPostgresSqlConnectionString());
	//	serviceCollection.AddSingleton<IEasyFlowDbConnection>(cnn);
	//}
}
