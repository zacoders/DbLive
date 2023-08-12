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
	[ExpectedException(typeof(EasyFlowSqlException))]
	public void EasyFlowSqlException_Expected()
	{
		var cnn = _factory.GetDeployer(DBEngine.MSSQL, _cnnString);
		var sql = "se_le_ct 1 as col";

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

	[TestMethod]
	public void Complex_WithTransaction()
	{
		var cnn = _factory.GetDeployer(DBEngine.MSSQL, _cnnString);
		
		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(@"
			drop table if exists dbo.Test
		");

		cnn.ExecuteNonQuery(@"
			create table dbo.Test (
				Id int identity
			  , Name nvarchar(128) not null
			  , CreatedUtc datetime2(0) not null
					constraint DEF_Test_CreatedUtc default(sysutcdatetime())
			)
		");

		cnn.ExecuteNonQuery(@"
			insert into dbo.Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		cnn.ExecuteNonQuery(@"
			select *
			from dbo.Test
		");

		cnn.ExecuteNonQuery(@"
			drop table if exists dbo.Test
		");

		cnn.CommitTransaction();
	}
}