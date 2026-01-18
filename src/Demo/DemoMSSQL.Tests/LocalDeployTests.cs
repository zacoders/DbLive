using DbLive;
using DbLive.MSSQL;
using DbLive.xunit;
using System.Reflection;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public class LocalDeployTests(ITestOutputHelper _output)
{
	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL;Trusted_Connection=True;";

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProject(Assembly.Load("DemoMSSQL"));

		IDbLive deployer = builder.CreateDeployer();

		await deployer.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = true
			}
		);
	}
}