using DbLive.PostgreSQL;
using DbLive.xunit;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;

public class DeploymentTests(ITestOutputHelper _output)
{
	/// <summary>
	/// Deploys the project to an empty database as production does, in three deployments:
	/// 1. migrations, code and the SQL tests;
	/// 2. the breaking phase and the SQL tests again;
	/// 3. the breaking phase once more, which must not change the schema.
	/// Then drops the database.
	/// </summary>
	[SqlDeployFact]
	public Task ReleaseDeploys() => new DeploymentTest(
		fixtureBuilder: new PostgreSqlContainerFixtureBuilder(typeof(DemoPostgreSQLChinookLink).Assembly),
		dropDatabaseOnComplete: true
	).DeployAsync(_output);
}
