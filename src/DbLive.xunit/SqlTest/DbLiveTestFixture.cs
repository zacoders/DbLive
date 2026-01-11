using DbLive.Adapter;
using DbLive.Common;
using DbLive.Testing;
using Xunit;

namespace DbLive.xunit.SqlTest;

public class DbLiveTestFixture(
		IDbLiveFixtureBuilder fixtureBuilder,
		bool dropDatabaseOnComplete
	)
	: IAsyncLifetime
{
	private IDbLive? _deployer;
	private DbLiveBuilder? _builder;

	public IDbLiveTester? Tester { get; private set; }

	public string ProjectPath { get; private set; } = fixtureBuilder.GetProjectPath();

	public async Task InitializeAsync()
	{
		_builder = await fixtureBuilder.GetBuilderAsync().ConfigureAwait(false);

		_deployer = _builder.CreateDeployer();

		await _deployer.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		}).ConfigureAwait(false);

		Tester = _builder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		if (dropDatabaseOnComplete && _builder is not null)
		{
			IDbLiveDA da = _builder.CreateDbLiveDA();
			await da.DropDbAsync().ConfigureAwait(false);
		}
	}
}
