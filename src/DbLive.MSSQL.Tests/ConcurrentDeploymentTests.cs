using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// One deployment of a database at a time: while a deployment holds the deployment lock, another one fails at once,
/// before it reads the applied migrations and runs them again.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ConcurrentDeploymentTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// the query of the docs ("One deployment at a time"): the session that holds the deployment lock.
	private const string LockHolderQuery = """
		select request_session_id
		from sys.dm_tran_locks
		where resource_type = 'APPLICATION'
		  and resource_database_id = db_id()
		  and resource_description like '%:\[dblive.deployment\]:%' escape '\';
		""";

	private const string LockIsFreeQuery = "select applock_test('public', 'dblive.deployment', 'Exclusive', 'Session');";

	[Fact]
	public async Task Second_deployment_fails_at_once_while_the_first_runs_a_migration()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create table dbo.Counter ( Note nvarchar(100) not null );
				create table dbo.Gate ( Id int not null );
				""");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		// a data migration: run twice, it inserts two rows without an error. It waits on the gate, which the test holds locked.
		_ = project.AddFile("Migrations/002.migration.sql", """
			insert into dbo.Counter ( Note ) values ( 'migration 002' );
			select count(*) from dbo.Gate;
			""");

		Task first;

		await using (SqlConnection gate = new(_dbCnnString))
		{
			await gate.OpenAsync();
			await using SqlTransaction gateTran = (SqlTransaction)await gate.BeginTransactionAsync();
			_ = await ScalarAsync<int>(gate, gateTran, "select count(*) from dbo.Gate with (tablockx, holdlock);");
			short gateSessionId = await ScalarAsync<short>(gate, gateTran, "select @@spid;");

			first = CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

			try
			{
				// the first deployment holds the deployment lock and runs migration 002.
				await WaitUntilAsync(
					async () => await ScalarAsync<int>(gate, gateTran, $"select count(*) from sys.dm_exec_requests where blocking_session_id = {gateSessionId};") > 0,
					first
				);

				// WaitAsync: a second deployment that waited for the lock would wait for the gate, and the test would hang.
				DeploymentInProgressException ex = await Assert.ThrowsAsync<DeploymentInProgressException>(
					() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default).WaitAsync(WaitLimit)
				);

				Assert.Equal(new SqlConnectionStringBuilder(_dbCnnString).InitialCatalog, ex.DatabaseName);
			}
			finally
			{
				await gateTran.RollbackAsync();
			}
		}

		await first;

		Assert.Equal(1, await ScalarAsync<int>("select count(*) from dbo.Counter;"));

		// the first deployment released the lock: the next one runs and has nothing to apply.
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(1, await ScalarAsync<int>("select count(*) from dbo.Counter;"));
	}

	[Fact]
	public async Task Lock_is_released_when_the_deployment_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Migrations/002.migration.sql", "select 1 / 0;");

		_ = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true })
		);

		Assert.Equal(1, await ScalarAsync<int>(LockIsFreeQuery));

		// the next deployment takes the lock and fails on the migration again, not on the lock.
		_ = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);
	}

	[Fact]
	public async Task Lock_of_a_session_that_ended_is_released()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		// the lock of a deployment that lost its connection: its session still runs on the server.
		IAsyncDisposable lostDeploymentLock = await builder.GetRequiredService<IDbLiveDA>().AcquireDeploymentLockAsync();

		_ = await Assert.ThrowsAsync<DeploymentInProgressException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		// what the docs tell to do: find the session that holds the lock, and end it.
		int holder = await ScalarAsync<int>(LockHolderQuery);
		_ = await ScalarAsync<object?>($"kill {holder};");
		await WaitUntilAsync(async () => await ScalarAsync<int>(LockIsFreeQuery) == 1);

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		// the release fails on the ended session, and the error is not thrown.
		await lostDeploymentLock.DisposeAsync();
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private async Task<T> ScalarAsync<T>(string query)
	{
		await using SqlConnection cnn = new(_dbCnnString);
		await cnn.OpenAsync();
		return await ScalarAsync<T>(cnn, null, query);
	}

	private static async Task<T> ScalarAsync<T>(SqlConnection cnn, SqlTransaction? tran, string query)
	{
		await using SqlCommand cmd = new(query, cnn, tran);
		object? value = await cmd.ExecuteScalarAsync();
		return (T)value!;
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> condition, Task? deployment = null)
	{
		DateTime deadline = DateTime.UtcNow + WaitLimit;

		while (!await condition())
		{
			if (deployment is { IsCompleted: true })
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
