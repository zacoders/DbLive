using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploySqlIntegrationPersistedTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationTestBase(
		output,
		_fixture.MasterDbConnectionString,
		dbName: "DbLive-PersistedTest",
		keepDatabaseAfterTests: true
	), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public async Task DeployProject_PersistedDbName()
	{
		await DbLiveDeployer.DeployAsync(DeployParameters.Default);
		await DbLiveDeployer.DeployAsync(DeployParameters.Breaking);
	}
}