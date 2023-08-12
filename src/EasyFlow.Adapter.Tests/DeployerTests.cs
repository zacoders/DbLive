namespace EasyFlow.Adapter.Tests;

[TestClass]
public class DeployerTests : IntegrationTestsBase
{
	private readonly string _cnnString = "Data Source=.;Initial Catalog=EasyFlow_MainTestDB;Integrated Security=True;";
	private readonly IAdapterFactory _factory = Resolve<IAdapterFactory>();
		
	[TestMethod]
	public void TransactionTest()
	{
		var deployer = _factory.GetDeployer(DBEngine.MSSQL);
		var sql = "select 1 as col";

		var cnn = deployer.OpenConnection(_cnnString);

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(sql);

		cnn.CommitTransaction();
	}

	[TestMethod]
	public void ExecuteNonQuery_Simple()
	{
		var deployer = _factory.GetDeployer(DBEngine.MSSQL);
		var sql = "select 1 as col";

		var cnn = deployer.OpenConnection(_cnnString);

		cnn.ExecuteNonQuery(sql);
	}
}