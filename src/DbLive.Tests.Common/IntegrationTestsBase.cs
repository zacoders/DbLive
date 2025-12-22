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
		Container.AddXUnitLogger(output);
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
		return GetServiceProvider().GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}