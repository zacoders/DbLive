using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// One deployment of a database at a time: while a deployment holds the deployment lock, another one fails at once,
/// before it reads the applied migrations and runs them again.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ConcurrentDeploymentTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output, createDb: false)
{
	private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

	// the query of the docs ("One deployment at a time"): the session that holds the deployment lock.
	private const string LockHolderQuery = """
		select pid
		from pg_locks
		where locktype = 'advisory'
		  and database = (select oid from pg_database where datname = current_database())
		  and classid = 25698 and objid = 1818850917 and objsubid = 1
		  and granted;
		""";

	[Fact]
	public async Task Second_deployment_fails_at_once_while_the_first_runs_a_migration()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create table counter ( note text not null );
				create table gate ( id int not null );
				""");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		// a data migration: run twice, it inserts two rows without an error. It waits on the gate, which the test holds locked.
		_ = project.AddFile("Migrations/002.migration.sql", """
			insert into counter ( note ) values ( 'migration 002' );
			select count(*) from gate;
			""");

		Task first;

		await using (NpgsqlConnection gate = new(DbCnnString))
		{
			await gate.OpenAsync();
			await using NpgsqlTransaction gateTran = await gate.BeginTransactionAsync();
			_ = await ScalarAsync<object?>(gate, "lock table gate in access exclusive mode;");
			int gatePid = await ScalarAsync<int>(gate, "select pg_backend_pid();");

			first = CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

			try
			{
				// the first deployment holds the deployment lock and runs migration 002. Not on the gate connection: pg_stat_activity
				// keeps the snapshot of its first read until the end of the transaction.
				await WaitUntilAsync(
					async () => await ScalarAsync<long>($"select count(*) from pg_stat_activity where {gatePid} = any(pg_blocking_pids(pid));") > 0,
					first
				);

				// WaitAsync: a second deployment that waited for the lock would wait for the gate, and the test would hang.
				DeploymentInProgressException ex = await Assert.ThrowsAsync<DeploymentInProgressException>(
					() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default).WaitAsync(WaitLimit)
				);

				Assert.Equal(DbName, ex.DatabaseName);
			}
			finally
			{
				await gateTran.RollbackAsync();
			}
		}

		await first;

		Assert.Equal(1, await ScalarAsync<long>("select count(*) from counter;"));

		// the first deployment released the lock: the next one runs and has nothing to apply.
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(1, await ScalarAsync<long>("select count(*) from counter;"));
	}

	[Fact]
	public async Task Deployments_that_create_the_database_at_the_same_time_deploy_or_fail_on_the_lock()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create table counter ( note text not null );
				insert into counter ( note ) values ( 'migration 001' );
				""");

		// deployments of a database that does not exist yet, started together: they find no database, and all of them create it.
		Task[] deployments = [.. Enumerable.Range(0, 8).Select(_ => Task.Run(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		))];

		int deployed = 0;

		foreach (Task deployment in deployments)
		{
			try
			{
				await deployment.WaitAsync(WaitLimit);
				deployed++;
			}
			// not the error of a create that lost the race (23505 on pg_database_datname_index): such a deployment goes on to the lock.
			catch (DeploymentInProgressException)
			{
			}
		}

		Assert.True(deployed > 0);
		Assert.Equal(1, await ScalarAsync<long>("select count(*) from counter;"));
	}

	[Fact]
	public async Task Lock_is_released_when_the_deployment_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 ( id int not null );")
			.AddFile("Migrations/002.migration.sql", "select 1 / 0;");

		_ = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		);

		Assert.Null(await ScalarAsync<int?>(LockHolderQuery));

		// the next deployment takes the lock and fails on the migration again, not on the lock.
		_ = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);
	}

	[Fact]
	public async Task Lock_of_a_session_that_ended_is_released()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		// the lock of a deployment that lost its connection: its session still runs on the server.
		IAsyncDisposable lostDeploymentLock = await builder.GetRequiredService<IDbLiveDA>().AcquireDeploymentLockAsync();

		_ = await Assert.ThrowsAsync<DeploymentInProgressException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		// what the docs tell to do: find the session that holds the lock, and end it. The timeout waits for the end of the session.
		int holder = (await ScalarAsync<int?>(LockHolderQuery)).GetValueOrDefault();
		Assert.True(await ScalarAsync<bool>($"select pg_terminate_backend({holder}, 5000);"));

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		// the release fails on the ended session; the deployment logs the error as a warning.
		_ = await Assert.ThrowsAnyAsync<NpgsqlException>(async () => await lostDeploymentLock.DisposeAsync());
	}

	[Fact]
	public async Task Lock_connection_sends_keep_alives_both_ways()
	{
		await using DeploymentLock deploymentLock = await AcquireLockOfDatabasePostgresAsync(Fixture.PostgresDBConnectionString);
		NpgsqlConnection cnn = (NpgsqlConnection)deploymentLock.Connection;

		// the client: a keep-alive of Npgsql after 30 seconds without traffic.
		Assert.Equal(30, new NpgsqlConnectionStringBuilder(cnn.ConnectionString).KeepAlive);

		// the server: the session of a deployer that lost its network ends within about a minute.
		Assert.Equal("30", await ScalarAsync<string>(cnn, "show tcp_keepalives_idle;"));
		Assert.Equal("10", await ScalarAsync<string>(cnn, "show tcp_keepalives_interval;"));
		Assert.Equal("3", await ScalarAsync<string>(cnn, "show tcp_keepalives_count;"));
	}

	[Fact]
	public async Task Keep_alive_of_the_connection_string_keeps_the_lock_through_idle_session_timeout()
	{
		// a keep-alive of the connection string stays on the lock connection, and the keep-alive of Npgsql is activity of the session
		// for idle_session_timeout: the session of the lock outlives a timeout of 2 seconds. Verified: with the keep-alive of DbLive
		// (30 seconds) instead, the session ends and the lock with it.
		string cnnString = new NpgsqlConnectionStringBuilder(Fixture.PostgresDBConnectionString)
		{
			KeepAlive = 1,
			Options = "-c idle_session_timeout=2000"
		}.ConnectionString;

		await using DeploymentLock deploymentLock = await AcquireLockOfDatabasePostgresAsync(cnnString);
		NpgsqlConnection cnn = (NpgsqlConnection)deploymentLock.Connection;
		Assert.Equal("2s", await ScalarAsync<string>(cnn, "show idle_session_timeout;"));
		int lockPid = await ScalarAsync<int>(cnn, "select pg_backend_pid();");

		await Task.Delay(TimeSpan.FromSeconds(5));

		await using NpgsqlConnection postgres = new(Fixture.PostgresDBConnectionString);
		await postgres.OpenAsync();
		Assert.Equal(lockPid, await ScalarAsync<int?>(postgres, LockHolderQuery));
	}

	/// <summary>
	/// The lock of the database <c>postgres</c>, which no other test deploys: the tests of the lock connection need no database of their own.
	/// </summary>
	private async Task<DeploymentLock> AcquireLockOfDatabasePostgresAsync(string cnnString)
	{
		DbLiveBuilder builder = new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(Output))
			.PostgreSQL()
			.SetDbConnection(cnnString);

		return (DeploymentLock)await builder.GetRequiredService<IDbLiveDA>().AcquireDeploymentLockAsync();
	}

	private async Task<T> ScalarAsync<T>(string query)
	{
		await using NpgsqlConnection cnn = new(DbCnnString);
		await cnn.OpenAsync();
		return await ScalarAsync<T>(cnn, query);
	}

	private static async Task<T> ScalarAsync<T>(NpgsqlConnection cnn, string query)
	{
		await using NpgsqlCommand cmd = new(query, cnn);
		object? value = await cmd.ExecuteScalarAsync();
		return value is null or DBNull ? default! : (T)value;
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> condition, Task deployment)
	{
		DateTime deadline = DateTime.UtcNow + WaitLimit;

		while (!await condition())
		{
			if (deployment.IsCompleted)
			{
				// the deployment ended before the condition: its error tells why.
				await deployment;
				Assert.Fail("The deployment ended before the condition was met.");
			}

			if (DateTime.UtcNow > deadline)
			{
				Assert.Fail($"The condition was not met in {WaitLimit}.");
			}

			await Task.Delay(100);
		}
	}
}
