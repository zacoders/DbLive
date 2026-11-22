using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DbLive.MSSQL.Tests;

[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploySqlIntegrationFullBreakingTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Fact]
	public async Task DeployProject_Full_Plus_Breaking()
	{
		DbLiveBuilder builder = CreateBuilder().SetProject(Assembly.Load("DemoMSSQL"));
		IDbLive deployer = builder.CreateDeployer();
		IDbLiveTester tester = builder.CreateTester();

		await deployer.DeployAsync(DeployParameters.Default);
		await tester.RunAllTestsAsync();

		await deployer.DeployAsync(DeployParameters.Breaking);
	}
}