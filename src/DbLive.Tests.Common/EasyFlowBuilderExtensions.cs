
namespace DbLive.Tests.Common;


public static class DbLiveBuilderExtensions
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

	//public static DbLiveBuilder AddTestingMsSqlConnection(this DbLiveBuilder builder)
	//{
	//	builder.Container.AddTestingMsSqlConnection();
	//	return builder;
	//}

	//public static void AddTestingMsSqlConnection(this IServiceCollection serviceCollection)
	//{
	//	var testConfig = new TestConfig();
	//	var cnn = new DbLiveDbConnection(testConfig.GetSqlServerConnectionString());
	//	serviceCollection.AddSingleton<IDbLiveDbConnection>(cnn);
	//}

	//public static void AddTestingPostgreSQLConnection(this IServiceCollection serviceCollection)
	//{
	//	var testConfig = new TestConfig();
	//	var cnn = new DbLiveDbConnection(testConfig.GetPostgresSqlConnectionString());
	//	serviceCollection.AddSingleton<IDbLiveDbConnection>(cnn);
	//}
}
