namespace EasyFlow.Tests;

[TestClass]
public class DeploySqlIntegrationTest : IntegrationTestsBase
{
	[TestMethod]
	public void DeployProject()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlowTestDB;Integrated Security=True;";
		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlowDeploy>();

		deploy.DeployProject(path, sqlConnectionString);
	}
}