namespace EasyFlow.Adapter.Tests;

[TestClass]
public class DeployerTests : IntegrationTestsBase
{
	string cnnString = "Data Source=.;Initial Catalog=EasyFlow_MainTestDB;Integrated Security=True;";
	IAdapterFactory factory = Resolve<IAdapterFactory>();
		
	[TestMethod]
	public void TransactionTest()
	{
		var deployer = factory.GetDeployer(DBEngine.MSSQL);
		var sql = "select 1 as col";

		var cnn = deployer.OpenConnection(cnnString);

		deployer.BeginTransaction(cnn, TransactionIsolationLevel.ReadCommitted);
		
		deployer.ExecuteNonQuery(cnn, sql);

		deployer.CommitTransaction(cnn);
	}

	[TestMethod]
	public void ExecuteNonQuery_Simple()
	{
		var deployer = factory.GetDeployer(DBEngine.MSSQL);
		var sql = "select 1 as col";

		var cnn = deployer.OpenConnection(cnnString);

		deployer.ExecuteNonQuery(cnn, sql);
	}
}