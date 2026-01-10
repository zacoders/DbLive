using DbLive;
using DbLive.Common;
using DbLive.MSSQL;

string connectionString = "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;";
string projectPath = Path.GetFullPath(typeof(Program).Assembly.GetName().Name!);

var sqlDeploy = new DbLiveBuilder()
	.SqlServer()
	.SetDbConnection(connectionString)
	.SetProjectPath(projectPath)
	.LogToConsole()
	.CreateDeployer();

await sqlDeploy.DeployAsync(parameters: DeployParameters.Default);

await sqlDeploy.DeployAsync(parameters: DeployParameters.BreakingAndTests);
