using EasyFlow.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace EasyFlow.xunit;


public static class EasyFlowBuilderExtensions
{
	public static IEasyFlowBuilder LogToXUnitOutput(this IEasyFlowBuilder builder, ITestOutputHelper output)
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
