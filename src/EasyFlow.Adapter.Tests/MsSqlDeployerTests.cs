namespace EasyFlow.Adapter.Tests;

public class MsSqlDeployerTests : IntegrationTestsBase
{
	private readonly string _cnnString = "Data Source=.;Initial Catalog=EasyFlowTestDB;Integrated Security=True;";
	private readonly IEasyFlowDeployer Deployer;

	public MsSqlDeployerTests(ITestOutputHelper output) : base(output)
	{
		Container.InitializeEasyFlow(DBEngine.MSSQL);
		Deployer = Resolve<IEasyFlowDeployer>();
		Deployer.CreateDB(_cnnString, skipIfExists: true);
	}

	private IEasyFlowSqlConnection GetConnection()
	{
		return Deployer.OpenConnection(_cnnString);
	}

	[Fact]
	public void TransactionTest_Simple()
	{
		var cnn = GetConnection();
		var sql = "select 1 as col";

		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));
		
		cnn.ExecuteNonQuery(sql);

		tran.Complete();
	}

	[Fact]
	public void ExecuteNonQuery_Simple()
	{
		var cnn = GetConnection();
		var sql = "select 1 as col";

		cnn.ExecuteNonQuery(sql);
	}

	[Fact]
	public void EasyFlowSqlException_Expected()
	{
		var cnn = GetConnection();
		var sql = "se_le_ct 1 as col";

		Assert.Throws<EasyFlowSqlException>(() => cnn.ExecuteNonQuery(sql));
	}

	[Fact]
	public void ExecuteNonQuery_MultiStatementMsSql()
	{
		var cnn = GetConnection();
		var sql = @"
			select 1 as col
			go
			select 2 as col
			go
			select 3 as col
		";

		cnn.ExecuteNonQuery(sql);
	}

	[Fact]
	public void Complex_WithTransaction()
	{		
		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));
		
		var cnn = GetConnection();

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

		tran.Complete();
	}

	[Fact]
	public void TransactionTest()
	{
		var cnn = GetConnection();

		cnn.ExecuteNonQuery(@"
			drop table if exists dbo.TestTran1;
		
			create table dbo.TestTran1 (
				id int not null
			  , name varchar(128) not null
			);
			
			insert into dbo.TestTran1 ( id, name )
			values ( 1, 'Test1' ), ( 2, 'Test2')
		");

		using (var tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			var cnn1 = GetConnection();
			
			cnn1.ExecuteNonQuery(@"
				insert into dbo.TestTran1 ( id, name )
				values ( 3, 'Test3' )
			");

			cnn1.ExecuteNonQuery(@"
				if ( select count(*) from dbo.TestTran1 ) != 3
					raiserror('Three rows in the tables is expected!', 16, 1);
			
				if not exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This row should exists.', 16, 1);
			");

			tran1.Dispose();
		}

		using (var tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			var cnn2 = GetConnection();
			
			cnn2.ExecuteNonQuery(@"
				if ( select count(*) from dbo.TestTran1 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");

			tran2.Complete();
		}

		cnn.ExecuteNonQuery("drop table if exists dbo.TestTran1;");
	}


	[Fact]
	public void TransactionTest_RollbackOnException()
	{
		var cnn = GetConnection();

		cnn.ExecuteNonQuery(@"
			drop table if exists dbo.TestTran2;
		
			create table dbo.TestTran2 (
				id int not null
			  , name varchar(128) not null
			);
			
			insert into dbo.TestTran2 ( id, name )
			values ( 1, 'Test1' ), ( 2, 'Test2')
		");

		try
		{
			using var tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));
			using var cnn1 = GetConnection();

			cnn1.ExecuteNonQuery(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 3, 'Test3' )
			");

			cnn1.ExecuteNonQuery(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 4, 'Test4' !! syntax error !! )
			");
		}
		catch
		{
			// just ignoring syntax error exeption, expected transaction rollback
		}

		using var tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));
		var cnn2 = GetConnection();
		
		cnn2.ExecuteNonQuery(@"
				if ( select count(*) from dbo.TestTran2 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran2 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");
		tran2.Complete();

		cnn.ExecuteNonQuery("drop table if exists dbo.TestTran2;");
	}
}