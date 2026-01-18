using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Xunit.Abstractions;

namespace DbLive.xunit;


public static class DbLiveBuilderExtensions
{
	public static DbLiveBuilder LogToXUnitOutput(this DbLiveBuilder builder, ITestOutputHelper output)
	{
		return builder.ConfigureServices(services =>
		{
			services.AddXUnitLogger(output);
		});
	}

	private static void AddXUnitLogger(this IServiceCollection serviceCollection, ITestOutputHelper output)
	{
		Logger logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();

		_ = serviceCollection.AddSingleton<ILogger>(logger);
	}
}
