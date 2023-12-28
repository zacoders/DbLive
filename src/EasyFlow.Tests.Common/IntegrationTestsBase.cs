using EasyFlow.Tests.Config;
namespace EasyFlow.Tests.Common;

public abstract class IntegrationTestsBase : TestBase
{
	protected static readonly IServiceCollection Container;
	
	static IntegrationTestsBase()
	{
		Container = new ServiceCollection();
		Container.AddSingleton<TestConfig>();
	}

	protected IntegrationTestsBase(ITestOutputHelper output) : base(output)
	{		
	}

	///// <summary>
	///// Returns ServiceProvider. Builds if needed or returns existing.
	///// </summary>
	///// <returns></returns>
	//protected IServiceProvider GetServiceProvider()
	//{
	//	serviceProvider ??= Container.BuildServiceProvider();
	//	return serviceProvider;
	//}

	protected static TService GetService<TService>()
	{
		return Container.BuildServiceProvider().GetService<TService>() ?? throw new Exception($"Cannot resolve {typeof(TService).Name}.");
	}
}