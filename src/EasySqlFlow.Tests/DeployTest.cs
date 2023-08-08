using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DeploySQLTest
{
    [TestMethod]
    public void FirstTest()
    {
        string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
        var deploy = new DeploySQL();
        deploy.Deploy(path);
	}
}