using DbLive.Common;
using DbLive.Testing;
using Xunit;

namespace DbLive.xunit;

public abstract class DbLiveTestingFixture (bool dropDatabaseOnComplete)
	: IAsyncLifetime
{
	private IDbLive? _deployer;

	public IDbLiveTester? Tester { get; private set; }

	public abstract Task<IDbLiveBuilder> GetBuilderAsync();

	public async Task InitializeAsync()
	{
		IDbLiveBuilder DbLiveBuilder = await GetBuilderAsync();

		_deployer = DbLiveBuilder.CreateDeployer();

		_deployer.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		});

		Tester = DbLiveBuilder.CreateTester();
	}

	public Task DisposeAsync()
	{
		if (dropDatabaseOnComplete) _deployer!.DropDatabase();

		return Task.CompletedTask;
	}
}
