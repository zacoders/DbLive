namespace EasySqlFlow.Tests;

[TestClass]
public class DeploySQLIntegrationTest : IntegrationTestsBase
{
	[TestMethod]
	public void FirstTest()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "...";

		var deploy = Resolve<DeploySQL>();

		deploy.DeployProject(path, sqlConnectionString);
	}
}