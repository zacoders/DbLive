using DbLive.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace DbLive.xunit;


public static class DbLiveBuilderExtensions
{
	public static IDbLiveBuilder LogToXUnitOutput(this IDbLiveBuilder builder, ITestOutputHelper output)
	{
		builder.Container.AddXUnitLogger(output);
		return builder;
	}

	private static void AddXUnitLogger(this IServiceCollection serviceCollection, ITestOutputHelper output)
	{
		var logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();

		serviceCollection.AddSingleton<ILogger>(logger);
	}
}
