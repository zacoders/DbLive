using EasyFlow.Adapter;

namespace EasyFlow.Tests;

[TestClass]
public class DeploySqlIntegrationTest : IntegrationTestsBase
{
	[TestMethod]
	public void DeployProject()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlow_MainTestDB;Integrated Security=True;";
		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlowDeploy>();

		deploy.DeployProject(path, sqlConnectionString);
	}
}