namespace EasyFlow.Adapter.PostgreSQL.Tests;

public class PgSqlDeployerTests : IntegrationTestsBase
{
	private readonly IEasyFlowDA _da;

	public PgSqlDeployerTests(ITestOutputHelper output) : base(output)
	{
		Container.InitializePostgreSQL();
		Container.InitializeEasyFlow();
		Container.AddTestingPostgreSQLConnection();

		_da = GetService<IEasyFlowDA>();

		_da.CreateDB();
	}


	[Fact]
	public void TransactionTest_Simple()
	{
		var sql = "select 1 as col";

		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		_da.ExecuteNonQuery(sql);

		tran.Complete();
	}

	[Fact]
	public void ExecuteNonQuery_Simple()
	{
		var sql = "select 1 as col";

		_da.ExecuteNonQuery(sql);
	}

	[Fact]
	public void EasyFlowSqlException_Expected()
	{
		var sql = "se_le_ct 1 as col";

		Assert.Throws<EasyFlowSqlException>(() => _da.ExecuteNonQuery(sql));
	}

	[Fact]
	public void ExecuteNonQuery_MultiStatementMsSql()
	{
		var sql = @"
			select 1 as col;
			
			select 2 as col;
		
			select 3 as col;
		";

		_da.ExecuteNonQuery(sql);
	}


	[Fact]
	public void Complex_WithTransaction()
	{
		using var tran = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1));

		_da.ExecuteNonQuery(@"
			drop sequence if exists public.s_test_id;
			drop table if exists Test;
		");

		_da.ExecuteNonQuery(@"
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

		_da.ExecuteNonQuery(@"
			insert into Test ( Name )
			values ( 'Test1' ), ( 'Test2')
		");


		_da.ExecuteNonQuery(@"
			select *
			from Test
		");

		_da.ExecuteNonQuery(@"
			drop table if exists Test
		");

		tran.Complete();
	}


	[Fact]
	public void TransactionTest()
	{
		_da.ExecuteNonQuery(@"
			drop table if exists TestTran1;
		
			create table TestTran1 (
				id int not null
			  , name varchar(128) not null
			);
			
			insert into TestTran1 ( id, name )
			values ( 1, 'Test1' ), ( 2, 'Test2')
		");


		using (var tran1 = TransactionScopeManager.Create(TranIsolationLevel.ReadCommitted, TimeSpan.FromMinutes(1)))
		{
			_da.ExecuteNonQuery(@"
				update TestTran1
				set name = 'new name' 
			");

			tran1.Dispose();
		}

		_da.ExecuteNonQuery(@"
			do $$ begin

				if ( select count(*) from TestTran1 ) != 2 then
					raise exception 'Two rows in the tables is expected!'; 
				end if;

				if exists ( select * from TestTran1 where name = 'new name' ) then
					raise exception 'Transaction doesn`t work!'; 
				end if;

			end $$;  
		");

		_da.ExecuteNonQuery("drop table if exists TestTran1;");
	}
}