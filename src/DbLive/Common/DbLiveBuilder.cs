namespace DbLive.Common;

public sealed class DbLiveBuilder
{
	private readonly List<Action<IServiceCollection>> _registrations = [];

	public DbLiveBuilder()
	{
		_registrations.Add(services =>
		{
			services.AddSingleton<ILogger>(Serilog.Core.Logger.None);
			services.InitializeDbLive();
		});
	}

	
	public DbLiveBuilder ConfigureServices(Action<IServiceCollection> configure)
	{
		_registrations.Add(configure);
		return this;
	}

	private ServiceProvider BuildServiceProvider()
	{
		var services = new ServiceCollection();

		foreach (Action<IServiceCollection> registration in _registrations)
		{
			registration(services);
		}

		return services.BuildServiceProvider();
	}

	public IDbLive CreateDeployer()
		=> BuildServiceProvider().GetRequiredService<IDbLive>();

	public IDbLiveDA CreateDbLiveDA()
		=> BuildServiceProvider().GetRequiredService<IDbLiveDA>();

	public IDbLiveTester CreateTester()
		=> BuildServiceProvider().GetRequiredService<IDbLiveTester>();

	public IDbLiveProject CreateProject()
		=> BuildServiceProvider().GetRequiredService<IDbLiveProject>();
}