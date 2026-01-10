using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MsSqlDeployerTests : IntegrationTestsBase, IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private readonly IDbLiveDA _da;

	public MsSqlDeployerTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper output) : base(output)
	{
		Container.InitializeMSSQL();

		var cnn = new DbLiveDbConnection(_fixture.MasterDbConnectionString.SetRandomDatabaseName());
		_ = Container.AddSingleton<IDbLiveDbConnection>(cnn);

		Container.InitializeDbLive();

		_da = GetService<IDbLiveDA>();
	}

	public async Task InitializeAsync()
	{
		await _da.CreateDBAsync(skipIfExists: true).ConfigureAwait(false);
	}

	public Task DisposeAsync()
	{
		return Task.CompletedTask;
	}

	[Fact]
	public async Task TransactionTest_Simple()
	{
		var sql = "select 1 as col";

		using TransactionScope tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		await _da.ExecuteNonQueryAsync(sql);

		tran.Complete();
	}

	[Fact]
	public async Task ExecuteNonQuery_Simple()
	{
		var sql = "select 1 as col";

		await _da.ExecuteNonQueryAsync(sql);
	}

	[Fact]
	public async Task DbLiveSqlException_Expected()
	{
		var sql = "se_le_ct 1 as col";

		_ = await Assert.ThrowsAsync<DbLiveSqlException>(() => _da.ExecuteNonQueryAsync(sql));
	}

	[Fact]
	public async Task ExecuteNonQuery_MultiStatementMsSql()
	{
		var sql = @"
			select 1 as col
			go
			select 2 as col
			go
			select 3 as col
		";

		await _da.ExecuteNonQueryAsync(sql);
	}

	[Fact]
	public async Task Complex_WithTransaction()
	{
		using TransactionScope tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		await _da.ExecuteNonQueryAsync(@"
			drop table if exists dbo.Test
		");

		await _da.ExecuteNonQueryAsync(@"
			create table dbo.Test (
				Id int identity
			  , Name nvarchar(128) not null
			  , CreatedUtc datetime2(0) not null
					constraint DEF_Test_CreatedUtc default(sysutcdatetime())
			)
		");

		await _da.ExecuteNonQueryAsync(@"
			insert into dbo.Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		await _da.ExecuteNonQueryAsync(@"
			select *
			from dbo.Test
		");

		await _da.ExecuteNonQueryAsync(@"
			drop table if exists dbo.Test
		");

		tran.Complete();
	}

	[Fact]
	public async Task TransactionTest()
	{
		await _da.ExecuteNonQueryAsync(@"
			drop table if exists dbo.TestTran1;
		
			create table dbo.TestTran1 (
				id int not null
			  , name varchar(128) not null
			);
			
			insert into dbo.TestTran1 ( id, name )
			values ( 1, 'Test1' ), ( 2, 'Test2')
		");

		using (TransactionScope tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			await _da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran1 ( id, name )
				values ( 3, 'Test3' )
			");

			await _da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran1 ) != 3
					raiserror('Three rows in the tables is expected!', 16, 1);
			
				if not exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This row should exists.', 16, 1);
			");

			tran1.Dispose();
		}

		using (TransactionScope tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			await _da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran1 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran1 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");

			tran2.Complete();
		}

		await _da.ExecuteNonQueryAsync("drop table if exists dbo.TestTran1;");
	}


	[Fact]
	public async Task TransactionTest_RollbackOnException()
	{
		await _da.ExecuteNonQueryAsync(@"
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
			using TransactionScope tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

			await _da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 3, 'Test3' )
			");

			await _da.ExecuteNonQueryAsync(@"
				insert into dbo.TestTran2 ( id, name )
				values ( 4, 'Test4' !! syntax error !! )
			");
		}
		catch
		{
			// just ignoring syntax error exception, expected transaction rollback
		}

		using (TransactionScope tran2 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			await _da.ExecuteNonQueryAsync(@"
				if ( select count(*) from dbo.TestTran2 ) != 2
					raiserror('Two rows in the tables is expected!', 16, 1);
			
				if exists ( select * from dbo.TestTran2 where name = 'Test3' )
					raiserror('This rows should not be available in this transaction.', 16, 1);
			");
			tran2.Complete();
		}

		await _da.ExecuteNonQueryAsync("drop table if exists dbo.TestTran2;");
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