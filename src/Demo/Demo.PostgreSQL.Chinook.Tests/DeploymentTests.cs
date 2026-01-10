using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using DbLive.xunit;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;


public class DeploymentTests(ITestOutputHelper _output, MyPostgreSQLFixture fixture)
	: IClassFixture<MyPostgreSQLFixture>
{
	[Theory]
	[InlineData(false, UndoTestMode.None)]
	[InlineData(true, UndoTestMode.None)]
	[InlineData(false, UndoTestMode.MigrationUndoMigration)]
	[InlineData(true, UndoTestMode.MigrationUndoMigration)]
	[InlineData(false, UndoTestMode.MigrationBreakingUndoMigration)]
	[InlineData(true, UndoTestMode.MigrationBreakingUndoMigration)]
	public async Task Deploy(bool breaking, UndoTestMode undoTestingMode)
	{
		_output.WriteLine($"Deploying with breaking={breaking}, undoTestingMode={undoTestingMode}");

		DbLiveBuilder builder = await fixture.GetBuilderAsync();

		IDbLive deployer = builder
			.LogToXUnitOutput(_output)
			.CreateDeployer();

		await deployer.DeployAsync(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = breaking,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true,
			UndoTestDeployment = undoTestingMode
		});
	}


	[Fact]
	[Trait("Category", "LocalOnly")]
	public async Task DeployToLocalSqlServerAsync()
	{
		string dbCnnString = "Host=localhost;Port=5433;Database=dblive_chinook;Username=postgres;Password=123123;";
		string projectPath = Path.GetFullPath(MyPostgreSQLFixture.SqlProjectName);

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.PostgreSQL()
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
