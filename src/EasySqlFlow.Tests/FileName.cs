
public abstract class TestsBase
{
	protected readonly Random _random = new();

	protected static IServiceCollection Container { get; } = new ServiceCollection();

	static TestsBase()
	{
	}

	protected static TService Resolve<TService>()
	{
		var serviceProvider = Container.BuildServiceProvider();
		return serviceProvider.GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}

public abstract class IntegrationTestsBase: TestsBase
{
	static IntegrationTestsBase()
	{
		Bootstrapper.Initialize(Container);
	}
}