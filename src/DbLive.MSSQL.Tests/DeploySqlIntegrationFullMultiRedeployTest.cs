using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploySqlIntegrationFullMultiRedeployTest(
	SqlServerIntegrationFixture _fixture,
	ITestOutputHelper output
) : SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public async Task DeployProject_Full_Plus_Breaking()
	{
		await DbLiveDeployer.DeployAsync(DeployParameters.Default);

		await DbLiveDeployer.DeployAsync(DeployParameters.Breaking);

		// Redeploy again

		await DbLiveDeployer.DeployAsync(DeployParameters.Default);

		await DbLiveDeployer.DeployAsync(DeployParameters.Breaking);
	}
}