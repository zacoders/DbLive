using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using Microsoft.Extensions.DependencyInjection;


var container = new ServiceCollection();
container.InitializeEasyFlow();
container.InitializeMSSQL();

var serviceProvider = container.BuildServiceProvider();

var sqlDeploy = serviceProvider.GetService<IEasyFlow>() ?? throw new Exception("Cannot init easy flow.");

string projectPath = Path.GetFullPath(typeof(Program).Assembly.GetName().Name ?? throw new Exception("Empty assembly name?"));

sqlDeploy.DeployProject(
	proejctPath: projectPath,
	sqlConnectionString: "Server=.;Database=Test1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;",
	DeployParameters.Default
);
