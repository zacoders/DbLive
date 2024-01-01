using EasyFlow;
using EasyFlow.Common;


var sqlDeploy = new EasyFlowBuilder()
	.LogToConsole()
	.SqlServer()
	.CreateDeployer();

sqlDeploy.DeployProject(
	projectPath: Path.GetFullPath(typeof(Program).Assembly.GetName().Name!),
	sqlConnectionString: "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;",
	parameters: DeployParameters.Default
);

sqlDeploy.DeployProject(
	projectPath: Path.GetFullPath(typeof(Program).Assembly.GetName().Name!),
	sqlConnectionString: "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;",
	parameters: DeployParameters.BreakingAndTests
);
