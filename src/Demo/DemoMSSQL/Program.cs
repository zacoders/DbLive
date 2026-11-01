using DbLive;
using DbLive.MSSQL;
using System.Reflection;

const string dbCnnString = "Server=localhost;Database=DbLive_DemoMSSQL;Trusted_Connection=True;";

DbLiveBuilder builder = new DbLiveBuilder()
	.LogToConsole()
	.SqlServer()
	.SetDbConnection(dbCnnString)
	.SetProject(Assembly.GetExecutingAssembly());

await builder.CreateDeployer().DeployAsync(new DeployParameters
{
	CreateDbIfNotExists = true,
	DeployCode = true,
	DeployMigrations = true
}).ConfigureAwait(false);

// the demo deploys to a local database. A production deployer does not run the tests.
await builder.CreateTester().RunAllTestsAsync().ConfigureAwait(false);
