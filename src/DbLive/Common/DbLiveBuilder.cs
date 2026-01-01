
namespace DbLive.Common;

public class DbLiveBuilder : IDbLiveBuilder
{
	public IServiceCollection Container { get; }

	public DbLiveBuilder()
	{
		Container = new ServiceCollection();
		Container.AddSingleton<IDbLiveBuilder>(this);		
		Container.AddSingleton<ILogger>(Serilog.Core.Logger.None); // empty logger by default
		Container.InitializeDbLive();
	}
}
