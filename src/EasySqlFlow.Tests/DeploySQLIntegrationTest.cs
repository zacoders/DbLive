using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DeploySQLIntegrationTest : TestsBase
{
    static DeploySQLIntegrationTest() 
    {
        Bootstrapper.Initialize(Container);
    }

    [TestMethod]
    public void FirstTest()
    {
        string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
        
        var deploy = Resolve<DeploySQL>();

		deploy.Deploy(path);
	}
}