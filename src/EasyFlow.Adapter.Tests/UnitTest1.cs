namespace EasyFlow.Adapter.Tests;

[TestClass]
public class DeployerTests : IntegrationTestsBase
{
	[TestMethod]
	public void TransactionTest()
	{
		var factory = Resolve<IAdapterFactory>();
		var deployer = factory.GetDeployer(DBEngine.MSSQL);
		string cnnString = "Data Source=.;Initial Catalog=EasyFlow_MainTestDB;Integrated Security=True;";
		var sql = "select 1 as col";

		var tran = deployer.BeginTransaction(cnnString, TransactionIsolationLevel.Serializable);

		deployer.ExecuteNonQuery(tran, sql, TimeSpan.FromSeconds(30));

		tran.Commit();
	}
}