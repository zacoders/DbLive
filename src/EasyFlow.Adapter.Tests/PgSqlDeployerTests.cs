namespace EasyFlow.Adapter.Tests;

[TestClass]
public class DeployerTests : IntegrationTestsBase
{
	private readonly string _cnnString = "Server=localhost;Port=5432;Database=EasyFlowTestDB;User ID=postgres;password=123123;";
	private readonly IAdapterFactory _factory = Resolve<IAdapterFactory>();
		
	[TestMethod]
	public void TransactionTest()
	{
		var cnn = _factory.GetDeployer(DBEngine.PostgreSql, _cnnString);
		var sql = "select 1 as col";

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(sql);

		cnn.CommitTransaction();
	}

	[TestMethod]
	public void ExecuteNonQuery_Simple()
	{
		var cnn = _factory.GetDeployer(DBEngine.PostgreSql, _cnnString);
		var sql = "select 1 as col";

		cnn.ExecuteNonQuery(sql);
	}

	[TestMethod]
	public void ExecuteNonQuery_MultiStatementMsSql()
	{
		var cnn = _factory.GetDeployer(DBEngine.PostgreSql, _cnnString);
		var sql = @"
			select 1 as col;
			
			select 2 as col;
		
			select 3 as col;
		";

		cnn.ExecuteNonQuery(sql);
	}
}