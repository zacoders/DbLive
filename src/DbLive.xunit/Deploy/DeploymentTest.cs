using DbLive.Adapter;
using DbLive.Schema;
using DbLive.Testing;
using Xunit.Abstractions;

namespace DbLive.xunit;

/// <summary>
/// The deployment test of a project, the test of <see cref="SqlDeployFactAttribute"/>. Not an xUnit fixture: the test creates it
/// with <c>new</c> and calls <see cref="DeployAsync"/>, which gets the database, deploys and drops it.
/// </summary>
/// <param name="fixtureBuilder">Gives the builder of a new database; <see cref="DeployAsync"/> disposes it.</param>
/// <param name="dropDatabaseOnComplete">Drops the database at the end of <see cref="DeployAsync"/>, also when the deployment fails.</param>
public sealed class DeploymentTest(
		IDbLiveFixtureBuilder fixtureBuilder,
		bool dropDatabaseOnComplete
	)
{
	/// <summary>
	/// Deploys the project to a new database as production does: the release, then the breaking phase, then the breaking
	/// phase again, which must not change the schema. The SQL tests run after the release and after the first breaking phase.
	/// Then drops the database when <c>dropDatabaseOnComplete</c> is true, also after a failed deployment, and disposes the builder.
	/// </summary>
	public async Task DeployAsync(ITestOutputHelper output)
	{
		DbLiveBuilder builder = await fixtureBuilder.GetBuilderAsync().ConfigureAwait(false);

		try
		{
			try
			{
				await DeployAsProductionAsync(builder.LogToXUnitOutput(output)).ConfigureAwait(false);
			}
			catch when (dropDatabaseOnComplete)
			{
				// the error of the deployment is the result of the test: an error of the drop does not replace it.
				await TryDropDatabaseAsync(builder, output).ConfigureAwait(false);
				throw;
			}

			if (dropDatabaseOnComplete)
			{
				await DropDatabaseAsync(builder).ConfigureAwait(false);
			}
		}
		finally
		{
			await builder.DisposeAsync().ConfigureAwait(false);
		}
	}

	private static async Task DeployAsProductionAsync(DbLiveBuilder builder)
	{
		IDbLive deployer = builder.CreateDeployer();
		IDbLiveTester tester = builder.CreateTester();

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

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
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

	private static async Task TryDropDatabaseAsync(DbLiveBuilder builder, ITestOutputHelper output)
	{
		try
		{
			await DropDatabaseAsync(builder).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			output.WriteLine($"The database was not dropped after the failed deployment: {ex}");
		}
	}

	private static Task DropDatabaseAsync(DbLiveBuilder builder)
		=> builder.GetRequiredService<IDbLiveDA>().DropDbAsync();
}
