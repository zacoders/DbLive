using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploySqlIntegrationPersistedTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString, dbName: "EasyFlow-PersistedTest"), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_PersistedDbName()
	{
		EasyFlow.Deploy(DeployParameters.Default);
		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}