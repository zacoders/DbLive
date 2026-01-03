
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeployAndUndoTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	//: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString),
	: IAssemblyFixture<SqlServerIntegrationFixture>
{	
	[Fact]
	public async Task Deploy_MSSQL_demo_and_undo_to_empty_database()
	{
		string projectMSSQL = Path.GetFullPath("DemoMSSQL");
		string projectMSSQLEmpty = Path.GetFullPath("DemoMSSQLEmpty");

		//string dbCnnString = _fixture.MasterDbConnectionString.SetRandomDatabaseName();
		string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL_UNDO;Trusted_Connection=True;";

		var deployer = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectMSSQL)
			.CreateDeployer();

		var deployerEmptyDb = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectMSSQLEmpty)
			.CreateDeployer();


		// First deploy the full project to create the database
		deployer.Deploy(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = true
			}
		);

		// Now undo the deployment back to empty database
		deployerEmptyDb.Deploy(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = false,
				DeployMigrations = true,
				RunTests = true,
				AllowDatabaseDowngrade = true
			}
		);

		// redeploy again
		deployer.Deploy(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = true
			}
		);
	}
}