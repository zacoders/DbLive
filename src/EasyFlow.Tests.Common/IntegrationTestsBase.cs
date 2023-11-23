namespace EasyFlow.Tests.Common;

public abstract class IntegrationTestsBase : TestBase
{
	protected IServiceCollection Container { get; }
	private IServiceProvider? serviceProvider;

	protected IntegrationTestsBase(ITestOutputHelper output) : base(output)
	{
		Container = new ServiceCollection();
	}

	/// <summary>
	/// Returns ServiceProvider. Builds if needed or returns existing.
	/// </summary>
	/// <returns></returns>
	protected IServiceProvider GetServiceProvider()
	{
		serviceProvider ??= Container.BuildServiceProvider();
		return serviceProvider;
	}

	protected TService GetService<TService>()
	{
		return GetServiceProvider().GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}