using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using DbLive.xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


public class DeployTest(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	[Fact]
	public async Task DeployAsync()
	{
		IDbLive deployer = (await fixture.GetBuilderAsync()).CreateDeployer();

		await deployer.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = false
			}
		);
	}

	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Server=localhost;Database=DbLive_AdventureWorks;Trusted_Connection=True;";
		string projectPath = Path.GetFullPath(MyDbLiveTestingMSSQLFixture.SqlProjectName);

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);

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