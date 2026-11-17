using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


// The timer of System.Transactions runs on the thread pool: while the other tests of the project run in parallel, it fired more
// than 17 seconds late, after the deployment had committed. Alone, the tests do not depend on the load of the others.
[CollectionDefinition(nameof(TransactionTimeoutTests), DisableParallelization = true)]
public class TransactionTimeoutTestsCollection;

/// <summary>
/// When the timeout of a transaction of DbLive runs out, System.Transactions aborts the transaction, and on SQL Server the next step in
/// the scope fails: the next batch, a new connection. DbLive makes that error <see cref="ExecutionTimeoutException"/> that names the
/// timeout of the transaction, with the error of the step inside.
/// </summary>
[Collection(nameof(TransactionTimeoutTests))]
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class TransactionTimeoutTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Deployment_that_runs_longer_than_DeploymentTimeout_fails_with_its_timeout_and_leaves_nothing()
	{
		// 20 batches of 0.5 seconds: the migration has 12 hours, the deployment 3 seconds. The batch after the end of the transaction
		// fails in SqlClient. 10 seconds, not 5: the timer of System.Transactions runs on the thread pool, and while all test projects run
		// it was once more than 2 seconds late, after the deployment had committed.
		string batches = string.Concat(Enumerable.Repeat("insert into dbo.T values ( 1 );\nwaitfor delay '00:00:00.500';\ngo\n", 20));
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment", "DeploymentTimeout": "00:00:03" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.T ( Id int );\ngo\n" + batches);

		DbLiveBuilder builder = CreateBuilder(project);

		ExecutionTimeoutException ex = await Assert.ThrowsAsync<ExecutionTimeoutException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal("Execution Timeout Expired. The deployment ran longer than its timeout of 00:00:03 (DeploymentTimeout).", ex.Message);
		Assert.Equal(TimeSpan.FromSeconds(3), ex.Timeout);
		// the error of the file where the time ran out is inside.
		MigrationDeploymentException fileEx = Assert.IsType<MigrationDeploymentException>(ex.InnerException);
		Assert.Contains("001.migration.sql", fileEx.Message);

		Assert.Equal(0, await CountAsync(builder, "select count(*) from sys.tables where name = 'T'"));
		Assert.Equal(0, await CountAsync(builder, "select count(*) from dblive.migration where status = 'applied'"));
	}

	[Fact]
	public async Task Setup_and_test_that_each_fit_into_UnitTestItemTimeout_pass_together()
	{
		// 4 seconds together, longer than UnitTestItemTimeout: each file has its own limit, and the transaction of the test twice that.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:03" }""")
			.AddFile("Tests/Slow/setup.test.sql", "waitfor delay '00:00:02';")
			.AddFile("Tests/Slow/slow.sql", "waitfor delay '00:00:02';");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/Slow/slow.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<int> CountAsync(DbLiveBuilder builder, string query)
	{
		List<SqlResult> results = await builder.GetRequiredService<IDbLiveDA>().QuerySqlAsync(query);
		return Convert.ToInt32(results.Single().Rows.Single()[0]);
	}
}
