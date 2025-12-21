using EasyFlow;
using EasyFlow.Common;
using EasyFlow.MSSQL;

namespace AdventureWorks.Tests;


public class DeployTest()
{
	[Fact]
	public async Task DeployToLocalSqlServerAsync()
	{
		string projectPath = Path.GetFullPath(MyEasyFlowTestingMSSQLFixture.SqlProjectName);

		string localSqlServerCnnString = "Server=localhost;Database=AdventureWorksEasyFlow;Trusted_Connection=True;";

		IEasyFlowBuilder builder = new EasyFlowBuilder()
			.LogToConsole() // TODO: Fails without LogToConsole()
			//.LogToXUnitOutput(Output) // not supported yet.
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