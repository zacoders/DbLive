using DbLive.Deployers.Code;
using DbLive.Deployers.Migrations;
using DbLive.Testing;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// A script that runs longer than its timeout fails with <see cref="ExecutionTimeoutException"/>, as on SQL Server. DbLive cancels the
/// script when its time runs out, and the cancel is the inner exception: OperationCanceledException with the error of the server,
/// 57014 "canceling statement due to user request", inside.
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
		OperationCanceledException cancelEx = Assert.IsAssignableFrom<OperationCanceledException>(timeoutEx.InnerException);
		PostgresException serverEx = Assert.IsType<PostgresException>(cancelEx.InnerException);
		Assert.Equal(PostgresErrorCodes.QueryCanceled, serverEx.SqlState);
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
	public async Task Timeout_of_a_sql_test_limits_the_statements_after_its_first_result()
	{
		// the server runs the second statement while the reader moves to its result.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:01" }""")
			.AddFile("Tests/slow.sql", "select 1 as a;\nselect pg_sleep(10);");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/slow.sql");

		Assert.False(result.IsSuccess);
		Assert.Contains($"{TimeoutMessage} 00:00:01.", result.ErrorMessage);
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
		_ = Assert.IsAssignableFrom<OperationCanceledException>(timeoutEx.InnerException);
	}

	[Fact]
	public async Task Script_that_raises_notices_fails_at_its_timeout()
	{
		// Npgsql starts its command timeout again on every notice: with the command timeout as the limit this script of 5 seconds ran to
		// its end (verified).
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "do $$ begin for i in 1..5 loop raise notice 'step %', i; perform pg_sleep(1); end loop; end $$;")
			.AddFile("Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None", "Timeout": "00:00:03" }""");

		DbLiveBuilder builder = CreateBuilder(project);

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(ex.InnerException);
		Assert.Equal(TimeSpan.FromSeconds(3), timeoutEx.Timeout);
	}

	[Fact]
	public async Task Sql_test_that_raises_notices_fails_at_its_timeout()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "UnitTestItemTimeout": "00:00:03" }""")
			.AddFile("Tests/notices.sql", "do $$ begin for i in 1..5 loop raise notice 'step %', i; perform pg_sleep(1); end loop; end $$;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/notices.sql");

		Assert.False(result.IsSuccess);
		Assert.Contains($"{TimeoutMessage} 00:00:03.", result.ErrorMessage);
		_ = Assert.IsType<ExecutionTimeoutException>(result.Exception);
	}
}
