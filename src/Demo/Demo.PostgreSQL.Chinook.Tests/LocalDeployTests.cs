using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using DbLive.xunit;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;


public class LocalDeployTests(ITestOutputHelper _output)
{
	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Host=localhost;Port=5433;Database=dblive_chinook555;Username=postgres;Password=123123;";

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.PostgreSQL()
			.SetDbConnection(dbCnnString)
			.SetProject(typeof(LinkMe).Assembly);

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
