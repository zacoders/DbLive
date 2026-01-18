using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using System.Reflection;

string dbCnnString = "Host=localhost;Port=5433;Database=dblive_chinook555;Username=postgres;Password=123123;";

var deployer = new DbLiveBuilder()
	.LogToConsole()
	.PostgreSQL()
	.SetDbConnection(dbCnnString)
	.SetProject(Assembly.GetExecutingAssembly())
	.CreateDeployer();

await deployer.DeployAsync(new DeployParameters{
	CreateDbIfNotExists = true,
	DeployCode = true,
	DeployMigrations = true,
	RunTests = true
}).ConfigureAwait(false);

public class LinkMe { }