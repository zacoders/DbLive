using DbLive;
using DbLive.PostgreSQL;
using System.Reflection;

string dbCnnString = "Host=localhost;Port=5433;Database=dblive_chinook555;Username=postgres;Password=123123;";

DbLiveBuilder builder = new DbLiveBuilder()
	.LogToConsole()
	.PostgreSQL()
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

public class DemoPostgreSQLChinookLink { }