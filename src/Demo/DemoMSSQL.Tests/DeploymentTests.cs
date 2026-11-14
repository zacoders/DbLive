using DbLive.MSSQL;
using DbLive.xunit;
using System.Reflection;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

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
		fixtureBuilder: new MsSqlContainerFixtureBuilder(Assembly.Load("DemoMSSQL")),
		dropDatabaseOnComplete: true
	).DeployAsync(_output);
}
