
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeployAndUndoTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	//: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString),
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private readonly string dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();
	//readonly string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL_UNDO;Trusted_Connection=True;";

	[Fact]
	public async Task Deploy_MSSQL_demo_and_undo_to_empty_database()
	{
		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProject(Assembly.Load("DemoMSSQL"));

		DbLiveBuilder builderEmptyDb = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProject(Assembly.Load(assemblyString: "DemoMSSQLEmpty"));

		IDbLive deployer = builder.CreateDeployer();
		IDbLive deployerEmptyDb = builderEmptyDb.CreateDeployer();


		// First deploy the full project to create the database
		await deployer.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true,
				RecreateDatabase = true
			}
		);

		// checking database state using unit db unit tests
		await builder.CreateTester().RunAllTestsAsync();


		// Now undo the deployment back to empty database
		await deployerEmptyDb.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = false,
				DeployMigrations = true,
				AllowDatabaseDowngrade = true
			}
		);

		// some special tests added, it will check that database is empty
		await builderEmptyDb.CreateTester().RunAllTestsAsync();

		// redeploy again
		await deployer.DeployAsync(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = true,
				DeployCode = true,
				DeployMigrations = true
			}
		);

		// checking database state using unit db unit tests
		await builder.CreateTester().RunAllTestsAsync();
	}
}