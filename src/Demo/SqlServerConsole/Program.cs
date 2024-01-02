using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using EasyFlow.Common;

string connectionString = "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;";
string projectPath = Path.GetFullPath(typeof(Program).Assembly.GetName().Name!);

var sqlDeploy = new EasyFlowBuilder()
	.SqlServer()
	.SetDbConnection(connectionString)
	.SetProjectPath(projectPath)
	.LogToConsole()
	.CreateDeployer();

sqlDeploy.Deploy(parameters: DeployParameters.Default);

sqlDeploy.Deploy(parameters: DeployParameters.BreakingAndTests);
