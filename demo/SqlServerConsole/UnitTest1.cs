using DbLive;
using DbLive.Common;
using DbLive.MSSQL;

namespace SqlServerConsole;

public class UnitTest1
{
	[Fact]
	public void Test1()
	{
		string connectionString = "Server=.;Database=Test8;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;";
		string projectPath = Path.GetFullPath(typeof(UnitTest1).Assembly.GetName().Name!);

		var sqlDeploy = new DbLiveBuilder()
			.SqlServer()
			.SetDbConnection(connectionString)
			.SetProjectPath(projectPath)
			.LogToConsole()
			.CreateDeployer();

		sqlDeploy.Deploy(parameters: DeployParameters.Default);

		sqlDeploy.Deploy(parameters: DeployParameters.BreakingAndTests);

	}
}