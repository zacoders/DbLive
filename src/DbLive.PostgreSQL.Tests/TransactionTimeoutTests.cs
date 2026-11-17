using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


// The timer of System.Transactions runs on the thread pool: while the other tests of the project run in parallel, it fired more
// than 17 seconds late, after the deployment had committed. Alone, the tests do not depend on the load of the others.
[CollectionDefinition(nameof(TransactionTimeoutTests), DisableParallelization = true)]
public class TransactionTimeoutTestsCollection;

/// <summary>
/// When the timeout of a transaction of DbLive runs out, System.Transactions aborts the transaction, and on PostgreSQL Npgsql cancels the
/// running command to roll back. The cancel has the shape of a command timeout, but it is not the timeout of the script: DbLive makes it
/// <see cref="ExecutionTimeoutException"/> that names the timeout of the transaction, with the error of the step inside.
/// </summary>
[Collection(nameof(TransactionTimeoutTests))]
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class TransactionTimeoutTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Deployment_that_runs_longer_than_DeploymentTimeout_fails_with_its_timeout_and_leaves_nothing()
	{
		// the migration has 12 hours, the deployment 3 seconds: Npgsql cancels the statement when the transaction ends.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment", "DeploymentTimeout": "00:00:03" }""")
			.AddFile("Migrations/001.migration.sql", "create table t ( id int );\ninsert into t values ( 1 );\nselect pg_sleep(20);");

		DbLiveBuilder builder = CreateBuilder(project);

		ExecutionTimeoutException ex = await Assert.ThrowsAsync<ExecutionTimeoutException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal("Execution Timeout Expired. The deployment ran longer than its timeout of 00:00:03 (DeploymentTimeout).", ex.Message);
		Assert.Equal(TimeSpan.FromSeconds(3), ex.Timeout);
		// the error of the file where the time ran out is inside: the cancel of Npgsql, not the timeout of the script.
		MigrationDeploymentException fileEx = Assert.IsType<MigrationDeploymentException>(ex.InnerException);
		Assert.Contains("001.migration.sql", fileEx.Message);
		Assert.DoesNotContain("The script ran longer", fileEx.Message);
		_ = Assert.IsType<DbLiveSqlException>(fileEx.InnerException);

		Assert.Equal(0, await CountAsync(builder, "select count(*) from information_schema.tables where table_name = 't'"));
		Assert.Equal(0, await CountAsync(builder, "select count(*) from dblive.migration where status = 'applied'"));
	}

	[Fact]
	public async Task Setup_and_test_that_each_fit_into_UnitTestItemTimeout_pass_together()
	{
		// 4 seconds together, longer than UnitTestItemTimeout: each file has its own limit, and the transaction of the test twice that.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:03" }""")
			.AddFile("Tests/Slow/setup.test.sql", "do $$ begin perform pg_sleep(2); end $$;")
			.AddFile("Tests/Slow/slow.sql", "do $$ begin perform pg_sleep(2); end $$;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/Slow/slow.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<long> CountAsync(DbLiveBuilder builder, string query)
	{
		List<SqlResult> results = await builder.GetRequiredService<IDbLiveDA>().QuerySqlAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
