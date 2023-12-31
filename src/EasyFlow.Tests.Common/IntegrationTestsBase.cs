namespace EasyFlow.Tests.Common;

public abstract class IntegrationTestsBase
{
	private ServiceProvider? _serviceProvider;
	protected readonly IServiceCollection Container;
	protected ITestOutputHelper Output { get; }

	protected IntegrationTestsBase(ITestOutputHelper output)
	{
		Output = output;

		Container = new ServiceCollection();

		var logger = new LoggerConfiguration()
			// add the xunit test output sink to the serilog logger
			// https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
			.WriteTo.TestOutput(output)
			.CreateLogger();

		Container.AddSingleton<ILogger>(logger);

		Container.AddSingleton<TestConfig>();
	}

	/// <summary>
	/// Returns ServiceProvider. Builds if needed or returns existing.
	/// </summary>
	/// <returns></returns>
	protected IServiceProvider GetServiceProvider()
	{
		_serviceProvider ??= Container.BuildServiceProvider();
		return _serviceProvider;
	}

	protected TService GetService<TService>()
	{
		return Container.BuildServiceProvider().GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}