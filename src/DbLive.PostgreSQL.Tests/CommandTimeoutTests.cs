using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// Npgsql reports a command timeout as NpgsqlException "Exception while reading from stream" with an inner TimeoutException,
/// not as PostgresException, so the cause is in the message only when DbLive takes it from the inner exception.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CommandTimeoutTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private const string TimeoutMessage = "Timeout during reading attempt";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveInternalDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Command_timeout_of_a_migration_is_in_the_exception()
	{
		// without a transaction only the command timeout applies, not the timeout of the transaction.
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "select pg_sleep(10);")
			.AddFile("Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None", "Timeout": "00:00:01" }""");

		DbLiveBuilder builder = CreateBuilder(project);

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Contains("001.migration.sql", ex.Message);
		Assert.Contains(TimeoutMessage, ex.Message);
		_ = Assert.IsType<DbLiveSqlException>(ex.InnerException);
		_ = Assert.IsType<NpgsqlException>(ex.InnerException.InnerException);
		_ = Assert.IsType<TimeoutException>(ex.InnerException.InnerException.InnerException);
	}

	[Fact]
	public async Task Command_timeout_of_a_sql_test_is_in_its_error()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:01" }""")
			.AddFile("Tests/slow.sql", "select pg_sleep(10);");

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
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}
}
