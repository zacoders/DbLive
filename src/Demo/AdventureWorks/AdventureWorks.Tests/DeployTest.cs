using EasyFlow;
using EasyFlow.Common;
using EasyFlow.MSSQL;
using EasyFlow.xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


public class DeployTest(ITestOutputHelper output)
{
	[Fact]
	public async Task DeployToLocalSqlServerAsync()
	{
		string projectPath = Path.GetFullPath(MyEasyFlowTestingMSSQLFixture.SqlProjectName);

		string localSqlServerCnnString = "Server=localhost;Database=AdventureWorksEasyFlow;Trusted_Connection=True;";

		IEasyFlowBuilder builder = new EasyFlowBuilder()
			.LogToXUnitOutput(output) 
			.SqlServer()
			.SetDbConnection(localSqlServerCnnString)
			.SetProjectPath(projectPath);

		var deployer = builder.CreateDeployer();

		deployer.Deploy(
			new DeployParameters
			{
				CreateDbIfNotExists = true,
				DeployBreaking = false,
				DeployCode = true,
				DeployMigrations = true,
				RunTests = false
			}
		);
	}
}