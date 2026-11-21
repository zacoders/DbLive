using DbLive.Adapter;
using DbLive.Deployers.Testing;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// 0096-87: a top-level <c>COMMIT</c> or <c>ROLLBACK</c> in a script ends the transaction of DbLive without an error, also after a
/// <c>BEGIN</c> of the script (only a warning in a transaction). DbLive compares the ID of the transaction at the end of the command of
/// the script and fails it; the error of the check rolls back what the script ran after the end. What a <c>COMMIT</c> committed stays.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ScriptEndsTransactionTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Theory]
	[InlineData("insert into t ( id ) values ( 1 ); rollback; insert into t ( id ) values ( 2 );", new string[0])]
	// what the script committed stays; the statements after the COMMIT are rolled back.
	[InlineData("insert into t ( id ) values ( 1 ); commit; insert into t ( id ) values ( 2 );", new[] { "1" })]
	[InlineData("insert into t ( id ) values ( 1 ); rollback; begin; insert into t ( id ) values ( 2 );", new string[0])]
	// a migration wrapped in its own transaction: BEGIN is a warning, and COMMIT commits the transaction of DbLive.
	[InlineData("begin; insert into t ( id ) values ( 1 ); insert into t ( id ) values ( 2 ); commit;", new[] { "1", "2" })]
	// a comment without a line end at the end of the script does not swallow the check.
	[InlineData("insert into t ( id ) values ( 1 ); rollback; insert into t ( id ) values ( 2 ); -- the end", new string[0])]
	public async Task Migration_that_ends_its_transaction_fails_and_is_not_recorded(string script, string[] rowsLeft)
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project
			.AddFile("Migrations/002.migration.sql", script)
			.AddFile("Migrations/003.migration.sql", "insert into t ( id ) values ( 3 );");

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));

		DbLiveTransactionEndedException inner = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);
		Assert.StartsWith("Error deploying Migrations/002.migration.sql file.", ex.Message);
		Assert.Contains(inner.Message, ex.Message);
		Assert.StartsWith(
			"The script ended the transaction that DbLive runs it in: it ran COMMIT or ROLLBACK, for example BEGIN ... COMMIT around the script",
			inner.Message
		);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(["001:applied", "002:none", "003:none"], await ListAsync(da, "select concat(lpad(version::text, 3, '0'), ':', status) from dblive.migration where item_type = 'migration'"));
		Assert.Equal(rowsLeft, await ListAsync(da, "select id::text from t"));
	}

	[Fact]
	public async Task Migration_that_ends_the_deployment_transaction_fails_and_leaves_nothing()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project.AddFile("Migrations/002.migration.sql", "insert into t ( id ) values ( 1 ); rollback; insert into t ( id ) values ( 2 );");

		// the next deployment fails the same way: nothing of the script stayed.
		for (int run = 1; run <= 2; run++)
		{
			MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));
			_ = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);

			IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
			Assert.Empty(await ListAsync(da, "select version::text from dblive.migration where version = 2"));
			Assert.Empty(await ListAsync(da, "select id::text from t"));
		}
	}

	[Fact]
	public async Task Code_file_that_ends_the_deployment_transaction_fails_at_once_and_leaves_nothing()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project
			.AddFile("Code/a.sql", "create or replace view v1 as select 1 as x; insert into t ( id ) values ( 1 ); rollback; insert into t ( id ) values ( 2 ); create or replace view v2 as select 2 as x;")
			.AddFile("Code/c.sql", "create or replace view v3 as select 3 as x;");

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => DeployAsync(project));

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Deploy code file error. File path: Code/a.sql. The file ended the deployment transaction, so it is not retried.", codeEx.Message);
		_ = Assert.IsType<DbLiveTransactionEndedException>(codeEx.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select id::text from t"));
		Assert.Empty(await ListAsync(da, "select relative_path from dblive.code"));
		Assert.Empty(await ListAsync(da, ViewsQuery));
	}

	[Theory]
	[InlineData("rollback", new string[0], new string[0])]
	// what the file committed stays: the first view and the first row.
	[InlineData("commit", new[] { "1" }, new[] { "v1" })]
	public async Task Code_file_that_ends_its_own_transaction_fails_at_once(string end, string[] rowsLeft, string[] viewsLeft)
	{
		// outside the deployment transaction the file runs between begin and commit of its own, as on SQL Server.
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project.AddFile("Code/a.sql", $"create or replace view v1 as select 1 as x; insert into t ( id ) values ( 1 ); {end}; insert into t ( id ) values ( 2 ); create or replace view v2 as select 2 as x;");

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => DeployAsync(project));

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Deploy code file error. File path: Code/a.sql. The file ended its transaction, so it is not retried.", codeEx.Message);
		_ = Assert.IsType<DbLiveTransactionEndedException>(codeEx.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(rowsLeft, await ListAsync(da, "select id::text from t"));
		Assert.Equal(viewsLeft, await ListAsync(da, ViewsQuery));
		Assert.Equal(["code/a.sql:error"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
	}

	[Fact]
	public async Task Code_file_in_its_own_transaction_is_applied_as_a_whole()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project.AddFile("Code/a.sql", "create or replace view v1 as select 1 as x; create or replace view v2 as select 2 as x;");

		await DeployAsync(project);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(["v1", "v2"], await ListAsync(da, ViewsQuery));
		Assert.Equal(["code/a.sql:applied"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
	}

	[Theory]
	[InlineData("commit", new[] { "100" })]
	[InlineData("rollback", new string[0])]
	public async Task Test_that_ends_its_transaction_fails_and_only_what_it_committed_stays(string end, string[] rowsLeft)
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t ( id int not null primary key );")
			.AddFile("Tests/end.sql", $"insert into t ( id ) values ( 100 ); {end}; insert into t ( id ) values ( 200 ); select 1 as x; select 'rows' as assert; select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/end.sql");

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.ErrorMessage);
		Assert.Equal(rowsLeft, await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select id::text from t"));
	}

	private const string ViewsQuery = "select table_name::text from information_schema.views where table_schema = 'public'";

	private async Task<TempProject> DeployTableAsync(TransactionWrapLevel level)
	{
		TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{level}}" }""")
			.AddFile("Migrations/001.migration.sql", "create table t ( id int not null primary key );");

		await DeployAsync(project);

		return project;
	}

	private Task DeployAsync(TempProject project) => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.QuerySqlAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
