using DbLive.Adapter;
using DbLive.Deployers.Code;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Outside the deployment transaction a code file runs in a transaction of its own: a file whose later <c>GO</c> batch fails leaves
/// none of its batches applied.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeFileTransactionTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Theory]
	[InlineData("")]
	// the error rolls back the transaction itself, and there is nothing left to roll back.
	[InlineData("set xact_abort on;\ngo\n")]
	public async Task Code_file_whose_later_batch_fails_leaves_none_of_its_batches(string prefix)
	{
		using TempProject project = new TempProject()
			.AddFile("Code/a.sql", prefix + "create or alter view dbo.v1 as select 1 as x;\ngo\ncreate or alter view dbo.v2 as select x from dbo.missing;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Invalid object name 'dbo.missing'.", Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select name from sys.views"));
		Assert.Equal(["code/a.sql:error"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));

		// the connection of the file went back to the pool without an open transaction, which would keep its locks.
		Assert.Equal(["0"], await ListAsync(da, "select count(*) from sys.dm_exec_sessions where database_id = db_id() and open_transaction_count > 0 and session_id <> @@spid"));
	}

	[Fact]
	public async Task Code_file_runs_at_the_configured_isolation_level_in_its_transaction()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionIsolationLevel": "Serializable" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.T ( level int, user_transactions int );")
			// not @@trancount: an insert without a transaction counts its own.
			.AddFile("Code/a.sql", """
				insert into dbo.T
				select
					transaction_isolation_level,
					(select count(*) from sys.dm_tran_session_transactions where session_id = @@spid and is_user_transaction = 1)
				from sys.dm_exec_sessions
				where session_id = @@spid;
				""");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// 4: serializable; 1: in a transaction that began before the file.
		Assert.Equal(["4:1"], await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select concat(level, ':', user_transactions) from dbo.T"));
	}

	[Fact]
	public async Task Statement_not_allowed_in_a_transaction_fails_in_a_code_file()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/a.sql", "alter database current set recovery simple;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal(
			"ALTER DATABASE statement not allowed within multi-statement transaction.",
			Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message
		);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
