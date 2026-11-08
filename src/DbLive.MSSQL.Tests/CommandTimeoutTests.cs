using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Deployers.Testing;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// SqlClient reports a command timeout as SqlException -2 "Execution Timeout Expired".
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CommandTimeoutTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string TimeoutMessage = "Execution Timeout Expired";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).CreateDbLiveDA().CreateDBAsync();

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
		Assert.Contains(TimeoutMessage, ex.Message);
		_ = Assert.IsType<DbLiveSqlException>(ex.InnerException);
		Assert.Equal(-2, ex.InnerException.Get<SqlException>()?.Number);
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
		Assert.Contains(TimeoutMessage, result.ErrorMessage);
		Assert.Contains(TimeoutMessage, result.Output);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}
}
