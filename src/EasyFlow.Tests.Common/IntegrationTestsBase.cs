namespace EasyFlow.Tests.Common;

public abstract class IntegrationTestsBase : TestBase
{
	protected IntegrationTestsBase(ITestOutputHelper output) : base(output)
	{
	}

	protected IServiceCollection Container { get; } = new ServiceCollection();

	protected TService Resolve<TService>()
	{
		IServiceProvider serviceProvider = Container.BuildServiceProvider();
		return serviceProvider.GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}