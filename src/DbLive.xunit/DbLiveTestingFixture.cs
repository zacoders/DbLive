using DbLive.Common;
using DbLive.Testing;
using Xunit;

namespace DbLive.xunit;

public abstract class DbLiveTestingFixture(bool dropDatabaseOnComplete)
	: IAsyncLifetime
{
	private IDbLive? _deployer;

	public IDbLiveTester? Tester { get; private set; }

	public abstract Task<DbLiveBuilder> GetBuilderAsync();

	public async Task InitializeAsync()
	{
		DbLiveBuilder builder = await GetBuilderAsync();

		_deployer = builder.CreateDeployer();

		await _deployer.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		});

		Tester = builder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		if (dropDatabaseOnComplete)
		{
			DbLiveBuilder builder = await GetBuilderAsync();
			var da = builder.CreateDbLiveDA();
			da.DropDB();
		}
	}
}
