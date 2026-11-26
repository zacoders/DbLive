using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A test class whose tests deploy a database of their own: xUnit creates the class for each test, so each test gets a new random
/// database name. The database is created before the test, unless <c>createDb</c> is false: the deployment of the test creates it, or the
/// test checks that none is created. It is dropped after the test, also a failed one, and the sessions still open in it end.
/// </summary>
/// <param name="dbName">A fixed name instead of a random one.</param>
/// <param name="keepDb">Keeps the database after the test. With <c>.WithReuse(true)</c> in <see cref="SqlServerIntegrationFixture"/>
/// the container stays after the run, and the next run deploys to the database of the previous one; without it the fixture removes
/// the container, and the database with it.</param>
public abstract class SqlServerDbTests(
	SqlServerIntegrationFixture fixture,
	ITestOutputHelper output,
	bool createDb = true,
	string? dbName = null,
	bool keepDb = false
) : IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	protected SqlServerIntegrationFixture Fixture { get; } = fixture;

	protected ITestOutputHelper Output { get; } = output;

	protected string DbCnnString { get; } = dbName is null
		? fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName()
		: fixture.MasterDbConnectionString.SetMsSqlDatabaseName(dbName);

	protected string DbName => new SqlConnectionStringBuilder(DbCnnString).InitialCatalog;

	public virtual async Task InitializeAsync()
	{
		if (!createDb) return;

		// DbLive deploys to an existing database without creating it.
		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().CreateDBAsync();
	}

	public virtual async Task DisposeAsync()
	{
		if (keepDb) return;

		await using DbLiveBuilder builder = CreateBuilder();
		await builder.GetRequiredService<IDbLiveDA>().DropDbAsync();
	}

	/// <summary>A builder of the database of the test, with the log in the output of the test and without a project.</summary>
	protected DbLiveBuilder CreateBuilder()
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(Output)
			.SqlServer()
			.SetDbConnection(DbCnnString);
	}

	protected DbLiveBuilder CreateBuilder(TempProject project) => CreateBuilder().SetScriptsFolder(project.Path);

	/// <summary>
	/// Drops a login of the test, whose pooled connections are idle in the pools of its connection strings: an open connection of the
	/// login blocks <c>drop login</c>, so its sessions are killed first. Not <c>SqlConnection.ClearAllPools()</c>: it also clears the pools
	/// of the tests running in parallel, and the next connection of their transactions is promoted to a distributed transaction.
	/// <c>kill</c> does not wait for the session to end: a killed session can stay in <c>sys.dm_exec_sessions</c> for a moment, and
	/// <c>drop login</c> then fails with "Could not drop login ... as the user is currently logged in".
	/// Cleanup only, so a failure does not fail the test: on CI, with all test projects running, <c>drop login</c> waited past the command
	/// timeout of 30 seconds (2026-11-26, the release run of 2.0.0). The login then stays in the container of the tests, under a name of
	/// its own; the failure goes to the output of the test.
	/// </summary>
	protected async Task DropLoginAsync(string login)
	{
		try
		{
			await using SqlConnection cnn = new(Fixture.MasterDbConnectionString);
			await cnn.OpenAsync();
			await using SqlCommand cmd = new($"""
				declare @sql nvarchar(max) = (
					select string_agg(concat('kill ', session_id, ';'), ' ')
					from sys.dm_exec_sessions
					where login_name = '{login}'
				);
				exec sys.sp_executesql @sql;

				declare @deadline datetime2 = dateadd(second, 10, sysutcdatetime());
				while exists (select * from sys.dm_exec_sessions where login_name = '{login}') and sysutcdatetime() < @deadline
					waitfor delay '00:00:00.100';

				drop login [{login}];
				""", cnn);
			_ = await cmd.ExecuteNonQueryAsync();
		}
		catch (SqlException e)
		{
			Output.WriteLine($"The login {login} was not dropped, it stays in the container: {e.Message}");
		}
	}

	protected async Task<bool> DbExistsAsync()
	{
		await using SqlConnection cnn = new(Fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new("select count(*) from sys.databases where name = @name", cnn);
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (int)(await cmd.ExecuteScalarAsync())! == 1;
	}
}
