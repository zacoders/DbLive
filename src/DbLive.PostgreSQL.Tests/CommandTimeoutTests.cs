using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// A script that runs longer than its timeout fails with <see cref="ExecutionTimeoutException"/>, as on SQL Server. Npgsql reports the
/// timeout as NpgsqlException "Exception while reading from stream" with an inner TimeoutException, which is its inner exception.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CommandTimeoutTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	private const string TimeoutMessage = "Execution Timeout Expired. The script ran longer than its timeout of";

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
		Assert.Contains($"{TimeoutMessage} 00:00:01.", ex.Message);
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(ex.InnerException);
		Assert.Equal(TimeSpan.FromSeconds(1), timeoutEx.Timeout);
		_ = Assert.IsType<NpgsqlException>(timeoutEx.InnerException);
		_ = Assert.IsType<TimeoutException>(timeoutEx.InnerException.InnerException);
	}

	[Fact]
	public async Task Command_timeout_of_a_sql_test_is_in_its_error()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:01" }""")
			.AddFile("Tests/slow.sql", "select pg_sleep(10);");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/slow.sql");

		Assert.False(result.IsSuccess);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", result.ErrorMessage);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", result.Output);
		_ = Assert.IsType<ExecutionTimeoutException>(result.Exception);
	}

	[Fact]
	public async Task Command_timeout_of_a_setup_file_names_the_setup_file()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:01" }""")
			.AddFile("Tests/Orders/setup.test.sql", "select pg_sleep(10);")
			.AddFile("Tests/Orders/select.sql", "select 1;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/Orders/select.sql");

		Assert.False(result.IsSuccess);
		Assert.StartsWith($"Setup file error (Tests/Orders/setup.test.sql): {TimeoutMessage} 00:00:01.", result.ErrorMessage);
		_ = Assert.IsType<ExecutionTimeoutException>(result.Exception);
	}

	[Fact]
	public async Task Code_file_whose_statements_together_run_longer_than_its_timeout_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "CodeItemTimeout": "00:00:03" }""")
			.AddFile("Code/a.sql", "select pg_sleep(2);\nselect pg_sleep(2);");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal($"Deploy code file error. File path: Code/a.sql{Environment.NewLine}{TimeoutMessage} 00:00:03.", codeEx.Message);
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(codeEx.InnerException);
		Assert.Equal($"{TimeoutMessage} 00:00:03.", timeoutEx.Message);
		_ = Assert.IsType<NpgsqlException>(timeoutEx.InnerException);
	}
}
