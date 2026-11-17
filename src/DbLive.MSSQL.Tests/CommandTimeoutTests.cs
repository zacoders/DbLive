using DbLive.Adapter;
using DbLive.Deployers.Code;
using DbLive.Deployers.Folder;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The timeout of a script is the timeout of the whole file, not of each <c>GO</c> batch: each batch gets the time left of the file as
/// its command timeout. A script that runs out of it fails with <see cref="ExecutionTimeoutException"/>; the timeout error of SqlClient
/// (SqlException -2 "Execution Timeout Expired") is its inner exception, and there is none when the time ran out between two batches.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CommandTimeoutTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string TimeoutMessage = "Execution Timeout Expired. The script ran longer than its timeout of";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Command_timeout_of_a_migration_is_in_the_exception()
	{
		// without a transaction only the command timeout applies, not the timeout of the transaction.
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "waitfor delay '00:00:10';")
			.AddFile("Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None", "Timeout": "00:00:01" }""");

		DbLiveBuilder builder = CreateBuilder(project);

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Contains("001.migration.sql", ex.Message);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", ex.Message);
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(ex.InnerException);
		Assert.Equal(TimeSpan.FromSeconds(1), timeoutEx.Timeout);
		Assert.Equal(-2, Assert.IsType<SqlException>(timeoutEx.InnerException).Number);
	}

	[Fact]
	public async Task Command_timeout_of_a_sql_test_is_in_its_error()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:01" }""")
			.AddFile("Tests/slow.sql", "waitfor delay '00:00:10';");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/slow.sql");

		Assert.False(result.IsSuccess);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", result.ErrorMessage);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", result.Output);
		_ = Assert.IsType<ExecutionTimeoutException>(result.Exception);
	}

	[Fact]
	public async Task Code_file_whose_batches_together_run_longer_than_its_timeout_fails_and_leaves_none_of_them()
	{
		// each batch fits into the timeout, both together do not: the last one gets the time left of the file, at most 2 seconds.
		// Not 2 seconds for the last one: waitfor may end up to 0.15 seconds early, and the time left is rounded up, so two batches of
		// 2 seconds can both run to their end.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "CodeItemTimeout": "00:00:03" }""")
			.AddFile("Code/a.sql", "create or alter view dbo.v1 as select 1 as x;\ngo\nwaitfor delay '00:00:02';\ngo\nwaitfor delay '00:00:02.500';");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Deploy code file error. File path: Code/a.sql", codeEx.Message);
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(codeEx.InnerException);
		Assert.Equal($"{TimeoutMessage} 00:00:03.", timeoutEx.Message);
		Assert.Equal(-2, Assert.IsType<SqlException>(timeoutEx.InnerException).Number);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select name from sys.views"));
		Assert.Equal(["code/a.sql:error"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
	}

	[Fact]
	public async Task Code_file_whose_time_runs_out_between_two_batches_fails_the_same_way()
	{
		// the first batch gets 3 seconds (2.1 rounded up) and ends after 2.5 seconds, past the time of the file: the second one does not run.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "CodeItemTimeout": "00:00:02.1" }""")
			.AddFile("Code/a.sql", "create or alter view dbo.v1 as select 1 as x;\ngo\nwaitfor delay '00:00:02.500';\ngo\ncreate or alter view dbo.v2 as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(codeEx.InnerException);
		Assert.Equal($"{TimeoutMessage} 00:00:02.1000000.", timeoutEx.Message);
		Assert.Null(timeoutEx.InnerException);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select name from sys.views"));
	}

	[Fact]
	public async Task Code_file_whose_batches_together_fit_into_its_timeout_deploys()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "CodeItemTimeout": "00:00:03" }""")
			.AddFile("Code/a.sql", "waitfor delay '00:00:01';\ngo\nwaitfor delay '00:00:01';\ngo\ncreate or alter view dbo.v1 as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["v1"], await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select name from sys.views"));
	}

	[Fact]
	public async Task AfterDeploy_file_whose_batches_together_run_longer_than_its_timeout_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "AfterDeployFolderTimeout": "00:00:03" }""")
			.AddFile("AfterDeploy/a.sql", "waitfor delay '00:00:02';\ngo\nwaitfor delay '00:00:02.500';");

		DbLiveBuilder builder = CreateBuilder(project);

		FolderDeploymentException ex = await Assert.ThrowsAsync<FolderDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal($"""
			Error deploying AfterDeploy/a.sql file.
			{TimeoutMessage} 00:00:03.
			""".ReplaceLineEndings(), ex.Message.ReplaceLineEndings());
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(ex.InnerException);
		Assert.Equal(-2, Assert.IsType<SqlException>(timeoutEx.InnerException).Number);
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
		List<SqlResult> results = await da.QuerySqlAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
