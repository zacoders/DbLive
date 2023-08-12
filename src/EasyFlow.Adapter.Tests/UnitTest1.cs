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

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(sql);

		cnn.CommitTransaction();
	}

	[TestMethod]
	public void ExecuteNonQuery_Simple()
	{
		var deployer = factory.GetDeployer(DBEngine.MSSQL);
		var sql = "select 1 as col";

		var cnn = deployer.OpenConnection(cnnString);

		cnn.ExecuteNonQuery(sql);
	}
}