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
	[ExpectedException(typeof(EasyFlowSqlException))]
	public void EasyFlowSqlException_Expected()
	{
		var cnn = _factory.GetDeployer(DBEngine.PostgreSql, _cnnString);
		var sql = "se_le_ct 1 as col";

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


	[TestMethod]
	public void Complex_WithTransaction()
	{
		var cnn = _factory.GetDeployer(DBEngine.PostgreSql, _cnnString);

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);

		cnn.ExecuteNonQuery(@"
			drop sequence if exists public.s_test_id;
			drop table if exists Test;
		");

		cnn.ExecuteNonQuery(@"
			create sequence if not exists public.s_test_id
			  increment 1
			  minvalue 1000
			  maxvalue 2147483647
			  start 1000
			  cache 1;

			create table Test (
				id int not null default nextval('public.s_test_id'::regclass)
			  , name varchar(128) not null
			  , created_utc timestamp without time zone not null default timezone('utc', now())
			);
		");

		cnn.ExecuteNonQuery(@"
			insert into Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		cnn.ExecuteNonQuery(@"
			select *
			from Test
		");

		cnn.ExecuteNonQuery(@"
			drop table if exists Test
		");

		cnn.CommitTransaction();
	}
}