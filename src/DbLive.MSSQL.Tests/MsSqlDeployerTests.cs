using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MsSqlDeployerTests : IntegrationTestsBase, IAssemblyFixture<SqlServerIntegrationFixture>
{
	private readonly IDbLiveDA _da;

	public MsSqlDeployerTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper output) : base(output)
	{
		Container.InitializeMSSQL();

		var cnn = new DbLiveDbConnection(_fixture.MasterDbConnectionString.SetRandomDatabaseName());
		Container.AddSingleton<IDbLiveDbConnection>(cnn);

		Container.InitializeDbLive();

		_da = GetService<IDbLiveDA>();

		_da.CreateDBAsync(skipIfExists: true);
	}

	[Fact]
	public void TransactionTest_Simple()
	{
		var sql = "select 1 as col";

		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		_da.ExecuteNonQueryAsync(sql);

		tran.Complete();
	}

	[Fact]
	public void ExecuteNonQuery_Simple()
	{
		var sql = "select 1 as col";

		_da.ExecuteNonQueryAsync(sql);
	}

	[Fact]
	public async Task DbLiveSqlException_Expected()
	{
		var sql = "se_le_ct 1 as col";

		await Assert.ThrowsAsync<DbLiveSqlException>(() => _da.ExecuteNonQueryAsync(sql));
	}

	[Fact]
	public void ExecuteNonQuery_MultiStatementMsSql()
	{
		var sql = @"
			select 1 as col
			go
			select 2 as col
			go
			select 3 as col
		";

		_da.ExecuteNonQueryAsync(sql);
	}

	[Fact]
	public void Complex_WithTransaction()
	{
		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		_da.ExecuteNonQueryAsync(@"
			drop table if exists dbo.Test
		");

		_da.ExecuteNonQueryAsync(@"
			create table dbo.Test (
				Id int identity
			  , Name nvarchar(128) not null
			  , CreatedUtc datetime2(0) not null
					constraint DEF_Test_CreatedUtc default(sysutcdatetime())
			)
		");

		_da.ExecuteNonQueryAsync(@"
			insert into dbo.Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		_da.ExecuteNonQueryAsync(@"
			select *
			from dbo.Test
		");

		_da.ExecuteNonQueryAsync(@"
			drop table if exists dbo.Test
		");

		tran.Complete();
	}

	[Fact]
	public void TransactionTest()
	{
		_da.ExecuteNonQueryAsync(@"
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
			_da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran1 ( id, name )
				values ( 3, 'Test3' )
			");

			_da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran1 ) != 3
					raiserror('Three rows in the tables is expected!', 16, 1);
			
				if not exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This row should exists.', 16, 1);
			");

			tran1.Dispose();
		}

		using (var tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			_da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran1 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");

			tran2.Complete();
		}

		_da.ExecuteNonQueryAsync("drop table if exists dbo.TestTran1;");
	}


	[Fact]
	public void TransactionTest_RollbackOnException()
	{
		_da.ExecuteNonQueryAsync(@"
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

			_da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 3, 'Test3' )
			");

			_da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 4, 'Test4' !! syntax error !! )
			");
		}
		catch
		{
			// just ignoring syntax error exception, expected transaction rollback
		}

		using (var tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			_da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran2 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran2 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");
			tran2.Complete();
		}

		_da.ExecuteNonQueryAsync("drop table if exists dbo.TestTran2;");
	}

	[Fact]
	public async Task ExecuteQuery()
	{
		var sql = @"
			select 10 as UserId, 'TestUser10' as Name
			
			select assert = 'rows'
			select 11 as UserId, 'TestUser11' as Name
		";

		List<SqlResult> results = await _da.ExecuteQueryMultipleAsync(sql);

		Assert.Equal(3, results.Count);
	}

	[Fact]
	public async Task ExecuteQuery_NoResult()
	{
		var sql = @"
			if not exists ( select 1 )
				throw 50001, 'Admin user must exists.', 0;
		";

		List<SqlResult> results = await _da.ExecuteQueryMultipleAsync(sql);

		Assert.Empty(results);
	}
}