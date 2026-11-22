using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// <c>DropDbAsync</c> drops a database that other connections use. <c>single_user with rollback immediate</c> alone waits about
/// 3 seconds for any session it has to end; the kill of the sessions before it takes that wait away.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MsSqlDADropDbTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Drops_a_database_in_use_without_waiting_for_rollback_immediate()
	{
		await using DbLiveBuilder builder = CreateBuilder();
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		await da.CreateDBAsync();

		// an idle connection of the pool, as DbLive leaves them.
		await using (SqlConnection pooled = new(DbCnnString))
		{
			await pooled.OpenAsync();
		}

		string unpooled = new SqlConnectionStringBuilder(DbCnnString) { Pooling = false }.ConnectionString;

		await using SqlConnection inTransaction = new(unpooled);
		await inTransaction.OpenAsync();
		await using SqlTransaction transaction = inTransaction.BeginTransaction();
		await using (SqlCommand create = new("create table dbo.t ( id int not null );", inTransaction, transaction))
		{
			_ = await create.ExecuteNonQueryAsync();
		}

		await using SqlConnection running = new(unpooled);
		await running.OpenAsync();
		await using SqlCommand wait = new("waitfor delay '00:01:00';", running) { CommandTimeout = 120 };
		Task<int> query = wait.ExecuteNonQueryAsync();

		Stopwatch stopwatch = Stopwatch.StartNew();
		await da.DropDbAsync(skipIfNotExists: false);
		stopwatch.Stop();

		Assert.False(await DbExistsAsync());
		_ = await Assert.ThrowsAsync<SqlException>(() => query);
		Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The drop took {stopwatch.ElapsedMilliseconds} ms.");
	}

	[Fact]
	public async Task Login_that_may_not_kill_sessions_drops_a_database_in_use()
	{
		string login = $"dblive-dbcreator-{Guid.NewGuid():N}";
		const string password = "Dbcreator-1234";

		// dbcreator owns the database it creates, without VIEW SERVER STATE and ALTER ANY CONNECTION.
		await ExecuteAsync(Fixture.MasterDbConnectionString, $"""
			create login [{login}] with password = '{password}', check_policy = off;
			alter server role dbcreator add member [{login}];
			""");

		try
		{
			// not pooled: a pooled connection of the login would block 'drop login'.
			string loginCnnString = new SqlConnectionStringBuilder(DbCnnString) { UserID = login, Password = password, Pooling = false }.ConnectionString;

			await using DbLiveBuilder builder = new DbLiveBuilder()
				.LogToXUnitOutput(Output)
				.SqlServer()
				.SetDbConnection(loginCnnString);
			IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
			await da.CreateDBAsync();

			await using SqlConnection inUse = new(loginCnnString);
			await inUse.OpenAsync();

			await da.DropDbAsync(skipIfNotExists: false);

			Assert.False(await DbExistsAsync());
		}
		finally
		{
			// a session of a connection closed just before can still be ending, and 'drop login' fails while it is.
			await ExecuteAsync(Fixture.MasterDbConnectionString, $"""
				declare @deadline datetime2 = dateadd(second, 10, sysutcdatetime());
				while exists (select * from sys.dm_exec_sessions where login_name = '{login}') and sysutcdatetime() < @deadline
					waitfor delay '00:00:00.100';

				drop login [{login}];
				""");
		}
	}

	private static async Task ExecuteAsync(string cnnString, string sql)
	{
		await using SqlConnection cnn = new(cnnString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new(sql, cnn);
		_ = await cmd.ExecuteNonQueryAsync();
	}
}
