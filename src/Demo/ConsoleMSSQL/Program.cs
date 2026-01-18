using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using System.Reflection;

string connectionString = "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;";

IDbLive sqlDeploy = new DbLiveBuilder()
	.SqlServer()
	.SetDbConnection(connectionString)
	.SetProject(Assembly.GetExecutingAssembly())
	.LogToConsole()
	.CreateDeployer();

await sqlDeploy.DeployAsync(parameters: DeployParameters.Default).ConfigureAwait(false);

await sqlDeploy.DeployAsync(parameters: DeployParameters.BreakingAndTests).ConfigureAwait(false);
