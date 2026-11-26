using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Xunit.Abstractions;

namespace DbLive.xunit;

/// <summary>
/// Configures a <see cref="DbLiveBuilder"/> in an xUnit test.
/// </summary>
public static class DbLiveBuilderExtensions
{
	/// <summary>
	/// Logs to the output of the xUnit test, which shows the log of DbLive next to the result of the test.
	/// </summary>
	/// <param name="builder">The builder instance to configure.</param>
	/// <param name="output">The output of the test, from its constructor.</param>
	/// <returns>The same builder instance for chaining.</returns>
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
