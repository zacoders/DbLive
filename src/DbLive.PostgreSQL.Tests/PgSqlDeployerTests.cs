using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class PgSqlDeployerTests : IntegrationTestsBase, IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly IDbLiveDA _da;

	public PgSqlDeployerTests(PostgreSqlFixture _fixture, ITestOutputHelper output) : base(output)
	{
		Container.InitializePostgreSQL();
		Container.InitializeDbLive();

		var cnn = new DbLiveDbConnection(_fixture.PostgresDBConnectionString);
		_ = Container.AddSingleton<IDbLiveDbConnection>(cnn);

		_da = GetService<IDbLiveDA>();
	}

	public async Task InitializeAsync()
	{
		await _da.CreateDBAsync().ConfigureAwait(false);
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
			select 1 as col;
			
			select 2 as col;
		
			select 3 as col;
		";

		await _da.ExecuteNonQueryAsync(sql);
	}


	[Fact]
	public async Task Complex_WithTransaction()
	{
		using TransactionScope tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		await _da.ExecuteNonQueryAsync(@"
			drop sequence if exists public.s_test_id;
			drop table if exists Test;
		");

		await _da.ExecuteNonQueryAsync(@"
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

		await _da.ExecuteNonQueryAsync(@"
			insert into Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		await _da.ExecuteNonQueryAsync(@"
			select *
			from Test
		");

		await _da.ExecuteNonQueryAsync(@"
			drop table if exists Test
		");

		tran.Complete();
	}


	[Fact]
	public async Task TransactionTest()
	{
		await _da.ExecuteNonQueryAsync(@"
			drop table if exists TestTran1;
		
			create table TestTran1 (
				id int not null
			  , name varchar(128) not null
			);
			
			insert into TestTran1 ( id, name )
			values ( 1, 'Test1' ), ( 2, 'Test2')
		");


		using (TransactionScope tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			await _da.ExecuteNonQueryAsync(@"
				update TestTran1
				set name = 'new name' 
			");

			tran1.Dispose();
		}

		await _da.ExecuteNonQueryAsync(@"
			do $$ begin

				if ( select count(*) from TestTran1 ) != 2 then
					raise exception 'Two rows in the tables is expected!'; 
				end if;

				if exists ( select * from TestTran1 where name = 'new name' ) then
					raise exception 'Transaction do not work!'; 
				end if;

			end $$;  
		");

		await _da.ExecuteNonQueryAsync("drop table if exists TestTran1;");
	}
}