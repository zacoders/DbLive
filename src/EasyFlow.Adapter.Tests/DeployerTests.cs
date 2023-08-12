namespace EasyFlow.Adapter.Tests;

[TestClass]
public class DeployerTests : IntegrationTestsBase
{
	private readonly string _cnnString = "Data Source=.;Initial Catalog=EasyFlow_MainTestDB;Integrated Security=True;";
	private readonly IAdapterFactory _factory = Resolve<IAdapterFactory>();
		
	[TestMethod]
	public void TransactionTest()
	{
		var cnn = _factory.GetDeployer(DBEngine.MSSQL, _cnnString);
		var sql = "select 1 as col";

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(sql);

		cnn.CommitTransaction();
	}

	[TestMethod]
	public void ExecuteNonQuery_Simple()
	{
		var cnn = _factory.GetDeployer(DBEngine.MSSQL, _cnnString);
		var sql = "select 1 as col";

		cnn.ExecuteNonQuery(sql);
	}
}