using EasyFlow;
using SqlServerConsole;


var sqlDeploy = Deploy.DeployToSqlServer();

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
