
namespace DbLive;

public sealed class DbLiveBuilder : IDisposable, IAsyncDisposable
{
	private readonly List<Action<IServiceCollection>> _registrations = [];
	private readonly object _sync = new();
	private ServiceProvider? _serviceProvider;
	private bool _disposed;

	public DbLiveBuilder()
	{
		_registrations.Add(services =>
		{
			services
				.AddSingleton<ILogger>(Serilog.Core.Logger.None)
				.InitializeDbLive();
		});
	}


	public DbLiveBuilder ConfigureServices(Action<IServiceCollection> configure)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		ServiceProvider? providerToDispose;
		lock (_sync)
		{
			_registrations.Add(configure);
			providerToDispose = _serviceProvider;
			_serviceProvider = null;
		}

		providerToDispose?.Dispose();
		return this;
	}

	private ServiceProvider BuildServiceProvider()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_sync)
		{
			if (_serviceProvider is not null)
			{
				return _serviceProvider;
			}

			var services = new ServiceCollection();

			foreach (Action<IServiceCollection> registration in _registrations)
			{
				registration(services);
			}

			_serviceProvider = services.BuildServiceProvider();
			return _serviceProvider;
		}
	}

	public IDbLive CreateDeployer()
		=> BuildServiceProvider().GetRequiredService<IDbLive>();

	public IDbLiveDA CreateDbLiveDA()
		=> BuildServiceProvider().GetRequiredService<IDbLiveDA>();

	public IDbLiveTester CreateTester()
		=> BuildServiceProvider().GetRequiredService<IDbLiveTester>();

	public IDbLiveProject CreateProject()
		=> BuildServiceProvider().GetRequiredService<IDbLiveProject>();

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		ServiceProvider? providerToDispose;
		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			providerToDispose = _serviceProvider;
			_serviceProvider = null;
		}

		providerToDispose?.Dispose();
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		ServiceProvider? providerToDispose;
		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			providerToDispose = _serviceProvider;
			_serviceProvider = null;
		}

		if (providerToDispose is not null)
		{
			await providerToDispose.DisposeAsync().ConfigureAwait(false);
		}

		GC.SuppressFinalize(this);
	}
}