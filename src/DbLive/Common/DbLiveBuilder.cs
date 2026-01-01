using DbLive.Deployers;

namespace DbLive.Common;

public class DbLiveBuilder : IDbLiveBuilder
{
	public IServiceCollection Container { get; }

	public DbLiveBuilder()
	{
		Container = new ServiceCollection();
		Container.AddSingleton<IDbLiveBuilder>(this);
		Container.InitializeDbLive();
	}

	public IDbLiveBuilder CloneBuilder()
	{
		DbLiveBuilder newBuilder = new();

		foreach (var serviceDescriptor in Container)
		{
			newBuilder.Container.Add(serviceDescriptor);
		}

		return newBuilder;
	}

	internal IDbLiveDeployer CreateInternalDeployer()
	{
		var serviceProvider = Container.BuildServiceProvider();
		return serviceProvider.GetService<IDbLiveDeployer>()!;
	}
}
