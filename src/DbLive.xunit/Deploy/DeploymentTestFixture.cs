using DbLive.Adapter;
using DbLive.Schema;
using DbLive.Testing;
using Xunit;
using Xunit.Abstractions;

namespace DbLive.xunit;


public sealed class DeploymentTestFixture(
		IDbLiveFixtureBuilder builderProvider,
		bool dropDatabaseOnComplete
	)
	: IAsyncLifetime
{
	private DbLiveBuilder? _builder;

	public async Task InitializeAsync()
	{
		_builder = await builderProvider.GetBuilderAsync().ConfigureAwait(false);
	}

	public async Task DisposeAsync()
	{
		if (_builder is null)
		{
			return;
		}

		try
		{
			if (dropDatabaseOnComplete)
			{
				IDbLiveDA da = _builder.CreateDbLiveDA();
				await da.DropDbAsync().ConfigureAwait(false);
			}
		}
		finally
		{
			await _builder.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Deploys the project to the database as production does: the release, then the breaking phase, then the breaking
	/// phase again, which must not change the schema. The SQL tests run after the release and after the first breaking phase.
	/// </summary>
	public async Task DeployAsync(ITestOutputHelper output)
	{
		if (_builder is null)
		{
			throw new InvalidOperationException("Builder not initialized.");
		}

		var deployer = _builder.LogToXUnitOutput(output).CreateDeployer();
		IDbLiveTester tester = _builder.CreateTester();

		// the release is deployed, its breaking changes are not: old application instances still run.
		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = false,
			DeployCode = true,
			DeployMigrations = true
		};

		await deployer.DeployAsync(deployParams).ConfigureAwait(false);
		await tester.RunAllTestsAsync().ConfigureAwait(false);

		// a separate breaking run, as in production: the breaking phase guard passes after a normal deployment.
		await deployer.DeployAsync(DeployParameters.Breaking).ConfigureAwait(false);
		await tester.RunAllTestsAsync().ConfigureAwait(false);

		IDbLiveInternalDA da = _builder.GetRequiredService<IDbLiveInternalDA>();
		SchemaSnapshot beforeSecondRun = await da.GetSchemaSnapshotAsync().ConfigureAwait(false);

		// the second breaking run: code breaking scripts run again (they must be idempotent),
		// and BeforeDeploy/AfterDeploy scripts run a third time.
		await deployer.DeployAsync(DeployParameters.Breaking).ConfigureAwait(false);

		SchemaDiff diff = SchemaDiff.Compare(beforeSecondRun, await da.GetSchemaSnapshotAsync().ConfigureAwait(false));

		if (!diff.IsEmpty)
		{
			throw new SchemaChangedException(
				"Deployment test: a second breaking deployment changed the schema. The breaking scripts of the Code folder and the BeforeDeploy/AfterDeploy scripts run again on it and must not change the schema:",
				diff
			);
		}
	}
}
