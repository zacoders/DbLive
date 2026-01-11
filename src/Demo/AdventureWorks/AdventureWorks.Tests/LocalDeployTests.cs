using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using DbLive.xunit;
using Xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;

public class LocalDeployTests(ITestOutputHelper _output)
{
	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Server=localhost;Database=DbLive_AdventureWorks;Trusted_Connection=True;";

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(Path.GetFullPath("AdventureWorks.Database"));

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