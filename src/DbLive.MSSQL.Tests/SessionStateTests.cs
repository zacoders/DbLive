using DbLive.Adapter;
using DbLive.Testing;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// SQL Server resets a session when a pooled connection is reused, also the session of a transaction that the next connection of the
/// same <c>TransactionScope</c> gets back: a <c>SET</c> option or a temporary table of one script does not reach the next. Not the
/// transaction isolation level, which <c>sp_reset_connection</c> keeps: DbLive sets it before each script, the level of the transaction
/// in a transaction, so that a script never runs at the level the script before it left.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SessionStateTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	private const string SessionLevel = "select transaction_isolation_level from sys.dm_exec_sessions where session_id = @@spid";

	private static string InsertSessionLevel(string script) =>
		$"insert into dbo.T select '{script}', transaction_isolation_level from sys.dm_exec_sessions where session_id = @@spid;";

	[Theory]
	// 2: read committed, 4: serializable; the level of the deployment transaction.
	[InlineData("ReadCommitted", "serializable", 2)]
	[InlineData("Serializable", "read committed", 4)]
	public async Task Isolation_level_of_a_migration_does_not_reach_the_next_scripts_in_the_deployment_transaction(
		string transactionIsolationLevel,
		string levelOfMigration,
		int expectedLevel
	)
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "Deployment", "TransactionIsolationLevel": "{{transactionIsolationLevel}}" }""")
			.AddFile("Migrations/001.migration.sql", $"""
				create table dbo.T ( script varchar(10) not null, level int not null );
				set transaction isolation level {levelOfMigration};
				""")
			.AddFile("Migrations/002.migration.sql", InsertSessionLevel("migration"))
			.AddFile("Code/a.sql", InsertSessionLevel("code"));

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			[$"code:{expectedLevel}", $"migration:{expectedLevel}"],
			await builder.GetRequiredService<IDbLiveDA>().ListAsync("select concat(script, ':', level) from dbo.T")
		);
	}

	// a transaction that began in snapshot goes back to it after a script changed its level. Through the data access: a deployment with
	// Snapshot in the deployment transaction fails for another reason (0096-164).
	[Fact]
	public async Task Isolation_level_of_a_script_does_not_reach_the_next_script_in_a_snapshot_transaction()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();
		await da.ExecuteSqlAsync($"alter database [{DbName}] set allow_snapshot_isolation on;");

		using TransactionScope scope = TransactionScopeManager.Create(TranIsolationLevel.Snapshot, TimeSpan.FromMinutes(1));

		await da.ExecuteSqlAsync("set transaction isolation level read committed;");

		// 5: snapshot.
		Assert.Equal(5, await da.ScalarAsync(SessionLevel));
	}

	[Fact]
	public async Task Isolation_level_of_setup_test_sql_does_not_reach_the_test()
	{
		// 4: serializable, TestsTransactionIsolationLevel by default; setup.test.sql sets repeatable read (3).
		using TempProject project = new TempProject()
			.AddFile("Tests/Session/setup.test.sql", "set transaction isolation level repeatable read;")
			.AddFile("Tests/Session/level.sql", $"""
				{SessionLevel};
				select 'rows' as assert;
				select 4 as transaction_isolation_level;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/Session/level.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	[Fact]
	public async Task Temporary_table_and_set_option_of_setup_test_sql_do_not_reach_the_test()
	{
		// 16384: XACT_ABORT in @@OPTIONS.
		using TempProject project = new TempProject()
			.AddFile("Tests/Session/setup.test.sql", "create table #setup_rows ( x int ); set xact_abort on;")
			.AddFile("Tests/Session/session.sql", """
				select iif(object_id('tempdb..#setup_rows') is null, 0, 1) as temp_table, iif(@@options & 16384 = 0, 0, 1) as xact_abort;
				select 'rows' as assert;
				select 0 as temp_table, 0 as xact_abort;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/Session/session.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	/// <summary>
	/// What the docs tell an application that runs DbLive in its own process: the connections of DbLive go back to the pool of its
	/// connection string at the level of the last test, and the next connection of the application with the same connection string
	/// gets one of them. Another <c>Application Name</c> is another pool.
	/// </summary>
	[Fact]
	public async Task Connection_with_the_same_connection_string_gets_the_isolation_level_of_the_tests_from_the_pool()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/t.sql", "select 1 as x; select 'rows' as assert; select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/t.sql");
		Assert.True(result.IsSuccess, result.ErrorMessage);

		// 4: serializable, TestsTransactionIsolationLevel by default.
		Assert.Equal(4, await ReadSessionLevelAsync(DbCnnString));
		Assert.Equal(2, await ReadSessionLevelAsync(new SqlConnectionStringBuilder(DbCnnString) { ApplicationName = "MyApp" }.ConnectionString));
	}

	private static async Task<int> ReadSessionLevelAsync(string connectionString)
	{
		await using SqlConnection cnn = new(connectionString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new(SessionLevel, cnn);
		return Convert.ToInt32(await cmd.ExecuteScalarAsync());
	}
}
