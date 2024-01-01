using EasyFlow.Common;
using EasyFlow;

namespace TestProject1;

public class EasyFlowTestingMSSQL : EasyFlowTesting
{
	public EasyFlowTestingMSSQL(string projectPath, string connectionString)
		:base(null)
	{
		var sqlDeploy = new EasyFlowBuilder()
			.SqlServer()
			.DbConnection(connectionString)
			.Project(projectPath)
			.LogToConsole()
			.CreateDeployer();
	}
}
