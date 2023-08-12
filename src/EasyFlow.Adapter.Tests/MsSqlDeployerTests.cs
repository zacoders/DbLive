namespace EasyFlow.Adapter.Tests;

[TestClass]
public class MsSqlDeployerTests : IntegrationTestsBase
{
	private readonly string _cnnString = "Data Source=.;Initial Catalog=EasyFlowTestDB;Integrated Security=True;";
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

	[TestMethod]
	public void ExecuteNonQuery_MultiStatementMsSql()
	{
		var cnn = _factory.GetDeployer(DBEngine.MSSQL, _cnnString);
		var sql = @"
			select 1 as col
			go
			select 2 as col
			go
			select 3 as col
		";

		cnn.ExecuteNonQuery(sql);
	}
}