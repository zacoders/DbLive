namespace EasyFlow.Tests.Common;

public abstract class IntegrationTestsBase : TestBase
{
	protected readonly IServiceCollection Container;
	private ServiceProvider? _serviceProvider;

	protected IntegrationTestsBase(ITestOutputHelper output) : base(output)
	{
		Container = new ServiceCollection();
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