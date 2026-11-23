using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// A failed code file writes no row to <c>dblive.code</c>: its row stays its last success. The error is in the log and in the message
/// of its <see cref="CodeDeploymentException"/>, with the details of the server that <see cref="DbLiveSqlException.SqlError"/> holds.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeFileFailureTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	[Fact]
	public async Task Failed_code_file_has_the_error_of_the_server_in_its_message_and_writes_no_row()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/public/get_user.sql", "create or replace function get_user() returns int language sql as $$ selec 1 $$;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		string message = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions)).Message.ReplaceLineEndings();
		Assert.StartsWith(
			"""
			Deploy code file error. File path: Code/public/get_user.sql
			PostgreSQL error:
			Severity: ERROR
			SqlState: 42601
			""".ReplaceLineEndings(),
			message
		);
		Assert.Contains("syntax error at or near \"selec\"", message);
		Assert.Contains("Position: ", message);

		Assert.Empty(await builder.GetRequiredService<IDbLiveDA>().GetCodeItemsAsync());
	}

	[Fact]
	public async Task Failed_code_file_keeps_the_row_of_its_last_success_and_runs_again_on_the_next_deployment()
	{
		using TempProject project = new TempProject().AddFile("Code/public/v1.sql", "create or replace view v1 as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		CodeItemDto success = Assert.Single(await da.GetCodeItemsAsync());

		_ = project.AddFile("Code/public/v1.sql", "create or replace view v1 as select x from missing;");

		for (int run = 0; run < 2; run++)
		{
			// a new builder each time, like a new run of a release pipeline: the failed content runs again.
			_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
				() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
			);

			CodeItemDto row = Assert.Single(await da.GetCodeItemsAsync());
			Assert.Equal(CodeItemStatus.Applied, row.Status);
			Assert.Equal(success.ContentHash, row.ContentHash);
			Assert.Equal(success.AppliedUtc, row.AppliedUtc);
		}
	}

	[Fact]
	public async Task Self_deploy_removes_the_failures_that_an_older_DbLive_recorded()
	{
		using TempProject project = new TempProject().AddFile("Code/public/v1.sql", "create or replace view v1 as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		long lastSelfMigration = await da.GetSelfMigrationVersionAsync();

		// the dblive tables before self-migration 10, with the failures that DbLive recorded then.
		await da.ExecuteSqlAsync("""
			alter table dblive.code add column error text null;
			alter table dblive.migration add column error text null;

			create table dblive.unit_test_run (
			    relative_path text not null,
			    content_hash bigint not null,
			    run_utc timestamptz not null,
			    execution_time_ms int not null,
			    pass boolean not null,
			    error text null,
			    constraint pk_unit_tests_results primary key (relative_path)
			);

			insert into dblive.code (relative_path, item_type, status, execution_time_ms, applied_utc, created_utc, content_hash, error)
			values ('code/public/failed.sql', 'code', 'error', 1, '2026-01-01', '2026-01-01', 1, 'relation "missing" does not exist')
			     , ('code/public/failed.breaking.sql', 'breaking', 'error', 1, '2026-01-01', '2026-01-01', 2, 'function p() does not exist');

			insert into dblive.unit_test_run (relative_path, content_hash, run_utc, execution_time_ms, pass, error)
			values ('tests/t1.sql', 1, '2026-01-01', 1, false, 'failed');

			update dblive.version set version = 9;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(lastSelfMigration, await da.GetSelfMigrationVersionAsync());
		// not marked removed: the rows are gone. The applied row stays.
		Assert.Equal(["code/public/v1.sql:applied"], await da.ListAsync("select concat(relative_path, ':', status) from dblive.code"));
		Assert.Equal(0, await da.ScalarAsync("""
			select count(*) from information_schema.columns
			where table_schema = 'dblive' and table_name in ('code', 'migration') and column_name = 'error'
			"""));
		Assert.Equal(0, await da.ScalarAsync("select count(*) from information_schema.tables where table_schema = 'dblive' and table_name = 'unit_test_run'"));
	}
}
