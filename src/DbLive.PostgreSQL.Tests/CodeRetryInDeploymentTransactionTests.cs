using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// With <c>TransactionWrapLevel: Deployment</c> every error aborts the PostgreSQL transaction. A code file runs under a savepoint,
/// and the rollback to it makes the transaction usable again, so a file that fails on a missing dependency is retried as without
/// a transaction.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeRetryInDeploymentTransactionTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	[Theory]
	// a view and a 'language sql' function check their references when they are created. A plpgsql function does not.
	[InlineData("create view a as select x from c;", "create view c as select 1 as x;")]
	[InlineData("create function a() returns int language sql as $$ select c() $$;", "create function c() returns int language sql as $$ select 1 $$;")]
	public async Task Code_file_that_fails_on_a_missing_dependency_is_retried(string fileA, string fileC)
	{
		using TempProject project = CreateProject(fileA, fileC);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(["a", "c", "m"], await ObjectsAsync(da));
		Assert.Equal(["code/a.sql:applied", "code/c.sql:applied"], await da.ListAsync("select concat(relative_path, ':', status) from dblive.code"));
		Assert.Equal(["1:applied"], await da.ListAsync("select concat(version, ':', status) from dblive.migration where item_type = 'migration'"));
	}

	[Fact]
	public async Task Statements_of_a_failed_attempt_are_rolled_back_before_the_retry()
	{
		// without the rollback to the savepoint the retry would fail on the table of the first attempt.
		using TempProject project = CreateProject(
			"create table a_log ( x int );\ncreate view a as select x from c;",
			"create view c as select 1 as x;"
		);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(["a", "a_log", "c", "m"], await ObjectsAsync(da));
	}

	[Fact]
	public async Task Code_file_that_fails_on_every_attempt_fails_the_deployment_and_nothing_is_committed()
	{
		using TempProject project = CreateProject(
			"create function a() returns int language sql as $$ selec 1 $$;",
			"create view c as select 1 as x;"
		);

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith("Deploy code file error. File path: Code/a.sql" + Environment.NewLine, codeEx.Message);
		Assert.Contains("syntax error at or near \"selec\"", Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		// neither the migration before it nor the file after it, and no row of the file.
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ObjectsAsync(da));
		Assert.Empty(await da.ListAsync("select version::text from dblive.migration"));
		Assert.Empty(await da.ListAsync("select relative_path from dblive.code"));
	}

	[Fact]
	public async Task Code_file_that_breaks_its_connection_fails_the_deployment_with_its_own_error()
	{
		// the rollback to the savepoint fails: the transaction is gone with the session.
		using TempProject project = CreateProject("select pg_terminate_backend(pg_backend_pid());", "create view c as select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith("Deploy code file error. File path: Code/a.sql. The error ended the deployment transaction, so the file is not retried." + Environment.NewLine, codeEx.Message);
		Assert.Equal("57P01: terminating connection due to administrator command", Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ObjectsAsync(da));
		Assert.Empty(await da.ListAsync("select version::text from dblive.migration"));
	}

	[Fact]
	public async Task Transaction_is_not_usable_after_an_error_of_a_statement()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();

		using TransactionScope scope = TransactionScopeManager.Create();

		// the question is asked after a script: a transaction of DbLive gets its ID before every script in it.
		await da.ExecuteNonQueryAsync("select 1;", TranIsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
		Assert.True(await da.IsTransactionUsableAsync());

		_ = await Assert.ThrowsAsync<DbLiveSqlException>(() => da.ExecuteSqlAsync("select 1/0;"));

		Assert.False(await da.IsTransactionUsableAsync());
	}

	[Fact]
	public async Task Transaction_is_usable_after_an_error_of_a_code_file_and_the_statements_of_the_file_are_undone()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();

		using TransactionScope scope = TransactionScopeManager.Create();

		await da.ExecuteCodeItemAsync("create table a ( x int );", TranIsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

		DbLiveSqlException ex = await Assert.ThrowsAsync<DbLiveSqlException>(() => da.ExecuteCodeItemAsync("create table c ( x int );\nselect 1/0;", TranIsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30)));
		Assert.Contains("division by zero", ex.Message);

		Assert.True(await da.IsTransactionUsableAsync());
		// the file before it stays, the table of the failed file is gone.
		Assert.Equal(["a"], await ObjectsAsync(da));
	}

	private static TempProject CreateProject(string fileA, string fileC)
	{
		return new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment" }""")
			.AddFile("Migrations/001.migration.sql", "create table m ( x int );")
			.AddFile("Code/a.sql", fileA)
			// not b.sql: that is the name of a breaking script.
			.AddFile("Code/c.sql", fileC);
	}

	private static Task<List<string>> ObjectsAsync(IDbLiveDA da)
	{
		return da.ListAsync("""
			select relname::text from pg_class where relnamespace = 'public'::regnamespace and relkind in ('r', 'v')
			union all
			select proname::text from pg_proc where pronamespace = 'public'::regnamespace
			""");
	}
}
