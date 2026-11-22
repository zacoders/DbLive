
namespace DbLive;

/// <summary>
/// Configures DbLive for one database and creates the deployer and the tester. It needs exactly one provider
/// (<c>.SqlServer()</c> or <c>.PostgreSQL()</c>), one connection (<see cref="DbLiveBuilderExtensions.SetDbConnection"/>) and one project
/// (<see cref="DbLiveBuilderExtensions.SetProject"/> or <see cref="DbLiveBuilderExtensions.SetScriptsFolder"/>);
/// when one of them is set twice, the last call wins.
/// </summary>
/// <example>
/// <code>
/// using DbLiveBuilder builder = new DbLiveBuilder()
///     .LogToConsole()
///     .SqlServer()
///     .SetDbConnection(connectionString)
///     .SetProject(typeof(MyDbMarker).Assembly);
///
/// await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
/// </code>
/// </example>
public sealed class DbLiveBuilder : IDisposable, IAsyncDisposable
{
	private readonly List<Action<IServiceCollection>> _registrations = [];
	private readonly object _sync = new();
	private ServiceProvider? _serviceProvider;
	private bool _disposed;

	/// <summary>
	/// An empty builder that logs nothing. Set the provider, the connection and the project with its extension methods.
	/// </summary>
	public DbLiveBuilder()
	{
		_registrations.Add(services =>
		{
			services
				.AddSingleton<ILogger>(Serilog.Core.Logger.None)
				.InitializeDbLive();
		});
	}


	/// <summary>
	/// Adds a registration to the container. Internal: the container is not part of the API; a user sets the logger
	/// with <see cref="DbLiveBuilderExtensions.LogTo"/>, the scripts with <see cref="DbLiveBuilderExtensions.SetProject"/>
	/// or <see cref="DbLiveBuilderExtensions.SetScriptsFolder"/>.
	/// </summary>
	internal DbLiveBuilder ConfigureServices(Action<IServiceCollection> configure)
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

	/// <summary>
	/// The deployer of the database. A configuration method called after it makes the next <c>Create*</c> call create the objects anew.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The builder is disposed.</exception>
	/// <exception cref="InvalidOperationException">The provider, the connection or the project is not set.</exception>
	public IDbLive CreateDeployer()
		=> GetRequiredService<IDbLive>();

	/// <summary>
	/// The tester that runs the SQL tests of the project against the deployed database. A deployment does not run them.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The builder is disposed.</exception>
	/// <exception cref="InvalidOperationException">The provider, the connection or the project is not set.</exception>
	public IDbLiveTester CreateTester()
		=> GetRequiredService<IDbLiveTester>();

	internal IDbLiveProject CreateProject()
		=> GetRequiredService<IDbLiveProject>();

	internal T GetRequiredService<T>() where T : notnull
		=> BuildServiceProvider().GetRequiredService<T>();

	/// <summary>
	/// Disposes the objects the builder created. The deployers and testers it created must not be used after it.
	/// A logger passed to <see cref="DbLiveBuilderExtensions.LogTo"/> is not disposed.
	/// </summary>
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

	/// <inheritdoc cref="Dispose"/>
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