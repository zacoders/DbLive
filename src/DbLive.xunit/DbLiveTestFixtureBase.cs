using DbLive.Adapter;
using DbLive.Common;
using DbLive.Testing;
using Xunit;

namespace DbLive.xunit;

public abstract class DbLiveTestFixtureBase(bool dropDatabaseOnComplete)
	: IAsyncLifetime
{
	private IDbLive? _deployer;

	public IDbLiveTester? Tester { get; private set; }

	public abstract Task<DbLiveBuilder> GetBuilderAsync();
	public abstract string GetProjectPath();

	public async Task InitializeAsync()
	{
		DbLiveBuilder builder = await GetBuilderAsync().ConfigureAwait(false);

		_deployer = builder.CreateDeployer();

		await _deployer.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		}).ConfigureAwait(false);

		Tester = builder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		if (dropDatabaseOnComplete)
		{
			DbLiveBuilder builder = await GetBuilderAsync().ConfigureAwait(false);
			IDbLiveDA da = builder.CreateDbLiveDA();
			await da.DropDBAsync().ConfigureAwait(false);
		}
	}
}
