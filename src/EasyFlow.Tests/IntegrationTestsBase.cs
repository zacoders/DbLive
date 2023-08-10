namespace EasyFlow.Tests;

public abstract class IntegrationTestsBase
{
	protected static IServiceCollection Container { get; } = new ServiceCollection();

	protected static TService Resolve<TService>()
	{
		var serviceProvider = Container.BuildServiceProvider();
		return serviceProvider.GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}

	static IntegrationTestsBase()
	{
		Container.InitializeEasyFlow();
	}
}