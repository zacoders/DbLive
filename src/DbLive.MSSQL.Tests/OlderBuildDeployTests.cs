using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// DbLive does not roll back a database: deploying an older build fails before it changes anything.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class OlderBuildDeployTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Deploying_an_older_build_fails_and_changes_nothing()
	{
		DbLiveBuilder builder = CreateBuilder().SetProject(Assembly.Load("DemoMSSQL"));

		// the older build: only migration 001.
		DbLiveBuilder olderBuilder = CreateBuilder().SetProject(Assembly.Load("DemoMSSQLEmpty"));

		await builder.CreateDeployer().DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true,
				RecreateDatabase = true
			}
		);

		await builder.CreateTester().RunAllTestsAsync();

		foreach (DeployParameters parameters in new[] { DeployParameters.Default, DeployParameters.Breaking })
		{
			DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(
				() => olderBuilder.CreateDeployer().DeployAsync(parameters)
			);

			Assert.Equal([2, 3, 4, 5, 6, 7], ex.Versions);
		}

		// the database is the one DemoMSSQL deployed: its SQL tests pass, and it deploys again.
		await builder.CreateTester().RunAllTestsAsync();

		await builder.CreateDeployer().DeployAsync(
			new DeployParameters
			{
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true
			}
		);

		await builder.CreateTester().RunAllTestsAsync();
	}
}
