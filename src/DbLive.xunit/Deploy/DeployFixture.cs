using DbLive.Adapter;
using DbLive.Common;
using DbLive.Schema;
using Xunit;
using Xunit.Abstractions;

namespace DbLive.xunit.Deploy;


public class DeployFixture(
		IDbLiveFixtureBuilder builderProvider,
		bool dropDatabaseOnComplete
	)
	: IAsyncLifetime
{
	public IDbLive? Deployer { get; private set; }

	private DbLiveBuilder? _builder;

	public async Task InitializeAsync()
	{
		_builder = await builderProvider.GetBuilderAsync().ConfigureAwait(false);
		Deployer = _builder.CreateDeployer();
	}

	public async Task DisposeAsync()
	{
		if (dropDatabaseOnComplete && _builder is not null)
		{
			IDbLiveDA da = _builder.CreateDbLiveDA();
			await da.DropDbAsync().ConfigureAwait(false);
		}
	}

	public async Task DeployAsync(ITestOutputHelper output, bool deployBreaking, UndoTestMode undoTestMode)
	{
		if (_builder is null)
		{
			throw new InvalidOperationException("Builder not initialized.");
		}

		var deployer = _builder.LogToXUnitOutput(output).CreateDeployer();

		DeployParameters deployParams = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = deployBreaking,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true,
			UndoTestDeployment = undoTestMode
		};

		await deployer.DeployAsync(deployParams).ConfigureAwait(false);

		if (deployBreaking)
		{
			IDbLiveDA da = _builder.CreateDbLiveDA();
			SchemaSnapshot beforeSecondRun = await da.GetSchemaSnapshotAsync().ConfigureAwait(false);

			// a separate breaking run, as in production: code breaking scripts run again (they must be idempotent),
			// the breaking phase guard passes after a normal deployment, and BeforeDeploy/AfterDeploy scripts run a second time.
			await deployer.DeployAsync(DeployParameters.Breaking).ConfigureAwait(false);

			SchemaDiff diff = SchemaDiff.Compare(beforeSecondRun, await da.GetSchemaSnapshotAsync().ConfigureAwait(false));

			if (!diff.IsEmpty)
			{
				throw new SchemaChangedException(
					SchemaCheck.BreakingSecondDeployment,
					"Deployment test: a second breaking deployment changed the schema. The breaking scripts of the Code folder and the BeforeDeploy/AfterDeploy scripts run again on it and must not change the schema:",
					diff
				);
			}
		}
	}
}
