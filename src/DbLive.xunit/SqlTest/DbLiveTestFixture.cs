using DbLive.Adapter;
using DbLive.Project;
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

	public IDbLiveProject GetProject()
	{
		DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(fixtureBuilder.GetProjectAssembly());
		return builder.CreateProject();
	}

	public async Task InitializeAsync()
	{
		_builder = await fixtureBuilder.GetBuilderAsync().ConfigureAwait(false);

		_deployer = _builder.CreateDeployer();

		Tester = _builder.CreateTester();

		await _deployer!.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		}).ConfigureAwait(false);
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
