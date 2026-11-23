using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A failed code file writes no row to <c>dblive.code</c>: its row stays applying, with the hash of its last success, as it was marked
/// before the run. The error is in the log and in the message of its <see cref="CodeDeploymentException"/>, with the details of the
/// server that <see cref="DbLiveSqlException.SqlError"/> holds.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeFileFailureTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	// SQL Server counts the line of an error from the start of its GO batch: the line above the errors names the line of the file
	// where the batch starts, also in a file of one batch.
	[Fact]
	public async Task Failed_code_file_has_the_error_of_the_server_with_the_line_where_its_batch_starts()
	{
		Assert.Equal(
			"""
			Deploy code file error. File path: Code/dbo/ErrorLine.sql
			SQL Server error:
			The batch starts at line 1 of the file.
			Msg 102, Level 15, State 1, Line 3
			Incorrect syntax near 'selec'.
			Procedure: GetUser
			""".ReplaceLineEndings(),
			await DeployFailingCodeFileAsync("create or alter proc dbo.GetUser\nas\nselec 1;")
		);
	}

	// the error is on line 5 of the file: the start of the batch plus its line in the batch, minus 1.
	[Fact]
	public async Task Error_in_the_second_batch_names_the_line_where_the_batch_starts()
	{
		Assert.Equal(
			"""
			Deploy code file error. File path: Code/dbo/ErrorLine.sql
			SQL Server error:
			The batch starts at line 3 of the file.
			Msg 8134, Level 16, State 1, Line 3
			Divide by zero error encountered.
			""".ReplaceLineEndings(),
			await DeployFailingCodeFileAsync("create or alter view dbo.ErrorLine as select 1 as a;\nGO\n\nselect 1;\nselect 1 / 0;")
		);
	}

	[Fact]
	public async Task Failed_code_file_with_a_long_error_has_all_of_it_in_its_message()
	{
		// every missing column is an error of its own: the error of the server is about 7000 characters.
		string columns = string.Join(", ", Enumerable.Range(1, 100).Select(i => $"missing_column_{i}"));

		string message = await DeployFailingCodeFileAsync($"create or alter view dbo.ManyErrors as select {columns} from ( select 1 as a ) t;");

		Assert.Contains("Invalid column name 'missing_column_1'.", message);
		Assert.Contains("Invalid column name 'missing_column_100'.", message);
	}

	// the row is applying, with the hash and the time of the last success: the failed content runs again, and so does the content of the
	// last success when it comes back (git revert, the previous release), since the failed run may have changed the database.
	[Fact]
	public async Task Failed_code_file_runs_again_on_the_next_deployment_also_with_the_content_of_its_last_success()
	{
		const string V1 = "create or alter view dbo.v1 as select 1 as x;";

		using TempProject project = new TempProject().AddFile("Code/dbo/v1.sql", V1);

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		CodeItemDto success = Assert.Single(await da.GetCodeItemsAsync());

		_ = project.AddFile("Code/dbo/v1.sql", "create or alter view dbo.v1 as select x from dbo.missing;");

		for (int run = 0; run < 2; run++)
		{
			// a new builder each time, like a new run of a release pipeline: the failed content runs again.
			_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
				() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
			);

			CodeItemDto row = Assert.Single(await da.GetCodeItemsAsync());
			Assert.Equal(CodeItemStatus.Applying, row.Status);
			Assert.Equal(success.ContentHash, row.ContentHash);
			Assert.Equal(success.AppliedUtc, row.AppliedUtc);
		}

		_ = project.AddFile("Code/dbo/v1.sql", V1);
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		// it ran: not skipped as [hash match].
		CodeItemDto restored = Assert.Single(await da.GetCodeItemsAsync());
		Assert.Equal(CodeItemStatus.Applied, restored.Status);
		Assert.Equal(success.ContentHash, restored.ContentHash);
		Assert.True(restored.AppliedUtc > success.AppliedUtc);
	}

	// a code file commits in a transaction of its own, and its applied row is written after it. When the write fails (here a trigger,
	// in production a broken connection, a failover, a stopped process), the object is the new content and the row the old one.
	// The row is applying, so the old content runs when it comes back, and restores the object.
	[Fact]
	public async Task Code_file_that_committed_without_its_row_runs_again_when_its_previous_content_comes_back()
	{
		const string V1 = "create or alter view dbo.v1 as select 1 as x;";

		using TempProject project = new TempProject().AddFile("Code/dbo/v1.sql", V1);

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		await da.ExecuteSqlAsync("""
			create trigger dblive.tr_lose_the_applied_row on dblive.code after update as
			if exists ( select 1 from inserted where status = 'applied' )
				throw 50000, 'The applied row is lost.', 1;
			""");

		_ = project.AddFile("Code/dbo/v1.sql", "create or alter view dbo.v1 as select 2 as x;");

		_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(["2"], await da.ListAsync("select x from dbo.v1"));
		Assert.Equal(CodeItemStatus.Applying, Assert.Single(await da.GetCodeItemsAsync()).Status);

		await da.ExecuteSqlAsync("drop trigger dblive.tr_lose_the_applied_row;");

		_ = project.AddFile("Code/dbo/v1.sql", V1);
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["1"], await da.ListAsync("select x from dbo.v1"));
		Assert.Equal(CodeItemStatus.Applied, Assert.Single(await da.GetCodeItemsAsync()).Status);
	}

	[Fact]
	public async Task Self_deploy_removes_the_failures_that_an_older_DbLive_recorded()
	{
		using TempProject project = new TempProject().AddFile("Code/dbo/v1.sql", "create or alter view dbo.v1 as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		long lastSelfMigration = await da.GetSelfMigrationVersionAsync();

		// the dblive tables before self-migration 13, with the failures that DbLive recorded then.
		await da.ExecuteSqlAsync("""
			alter table dblive.code add error nvarchar(4000) null;
			alter table dblive.migration add error nvarchar(4000) null;

			create table dblive.unit_test_run (
				relative_path nvarchar(4000) not null
			  , content_hash bigint not null
			  , run_utc datetime2(7) not null
			  , execution_time_ms int not null
			  , pass bit not null
			  , error nvarchar(4000) null
			  , constraint pk_unit_tests_results primary key ( relative_path )
			);
			go

			insert into dblive.code ( relative_path, item_type, status, execution_time_ms, applied_utc, created_utc, content_hash, error )
			values ( 'code/dbo/failed.sql', 'code', 'error', 1, '2026-01-01', '2026-01-01', 1, 'Invalid object name ''dbo.missing''.' )
			     , ( 'code/dbo/failed.breaking.sql', 'breaking', 'error', 1, '2026-01-01', '2026-01-01', 2, 'Cannot drop the procedure ''dbo.p''.' );

			insert into dblive.unit_test_run ( relative_path, content_hash, run_utc, execution_time_ms, pass, error )
			values ( 'tests/t1.sql', 1, '2026-01-01', 1, 0, 'failed' );

			update dblive.version set version = 12;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(lastSelfMigration, await da.GetSelfMigrationVersionAsync());
		// not marked removed: the rows are gone. The applied row stays.
		Assert.Equal(["code/dbo/v1.sql:applied"], await da.ListAsync("select concat(relative_path, ':', status) from dblive.code"));
		Assert.Equal(0, await da.ScalarAsync("select count(*) from sys.columns where name = 'error' and object_id in ( object_id('dblive.code'), object_id('dblive.migration') )"));
		Assert.Equal(0, await da.ScalarAsync("select count(*) from sys.tables where object_id = object_id('dblive.unit_test_run')"));
	}

	private async Task<string> DeployFailingCodeFileAsync(string script)
	{
		using TempProject project = new TempProject().AddFile("Code/dbo/ErrorLine.sql", script);

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		// a failure writes no row.
		Assert.Empty(await builder.GetRequiredService<IDbLiveDA>().GetCodeItemsAsync());

		return Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions)).Message.ReplaceLineEndings();
	}
}
