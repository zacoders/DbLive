using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// With <c>TransactionWrapLevel: Deployment</c> a failed code file is retried inside the deployment transaction. Some errors roll that
/// transaction back on SQL Server, and the next commands then run in autocommit mode: a retry and the files after it would be committed
/// while the migrations before them are rolled back. Such a file is not retried.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeRetryInDeploymentTransactionTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Fact]
	public async Task Code_file_that_fails_on_a_missing_dependency_is_retried()
	{
		using TempProject project = CreateProject("create view dbo.A as select x from dbo.C;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(["M"], await da.ListAsync("select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Equal(["A", "C"], await da.ListAsync("select name from sys.views"));
		Assert.Equal(["code/a.sql:applied", "code/c.sql:applied"], await da.ListAsync("select concat(relative_path, ':', status) from dblive.code"));
	}

	[Theory]
	// the dependency of a.sql is deployed after it, but with xact_abort its error rolls back the transaction.
	[InlineData("set xact_abort on;\ngo\ncreate view dbo.A as select x from dbo.C;", "Invalid object name 'dbo.C'.")]
	[InlineData("set xact_abort on; select 1/0;", "Divide by zero error encountered.")]
	// a failed conversion aborts the batch and rolls back the transaction without xact_abort too.
	[InlineData("select convert(int, 'abc');", "Conversion failed when converting the varchar value 'abc' to data type int.")]
	public async Task Code_file_whose_error_rolled_back_the_transaction_fails_the_deployment_at_once(string fileA, string error)
	{
		using TempProject project = CreateProject(fileA);

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith("Deploy code file error. File path: Code/a.sql. The error ended the deployment transaction, so the file is not retried." + Environment.NewLine, codeEx.Message);
		Assert.Equal(error, Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		// nothing is committed: neither the migration before the error nor the file after it.
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await da.ListAsync("select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Empty(await da.ListAsync("select name from sys.views"));
		Assert.Empty(await da.ListAsync("select version from dblive.migration"));
		Assert.Empty(await da.ListAsync("select relative_path from dblive.code"));
	}

	[Fact]
	public async Task Code_file_that_fails_inside_its_own_transaction_fails_the_deployment_at_once_with_that_reason()
	{
		// a statement error leaves the deployment transaction usable, but the transaction of the file is open, and SQL Server cannot roll
		// back a nested transaction alone: DbLive rolls back the whole one. The message said that the error ended it.
		using TempProject project = CreateProject("begin tran; exec dbo.Missing;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith(
			"Deploy code file error. File path: Code/a.sql. The file failed inside a transaction that it began, "
			+ "and DbLive rolled back the deployment transaction with it, so it is not retried." + Environment.NewLine,
			codeEx.Message
		);
		DbLiveSqlException sqlEx = Assert.IsType<DbLiveSqlException>(codeEx.InnerException);
		Assert.Equal("Could not find stored procedure 'dbo.Missing'.", sqlEx.Message);
		Assert.True(sqlEx.RolledBackWithTransactionLeftOpen);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await da.ListAsync("select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Empty(await da.ListAsync("select name from sys.views"));
		Assert.Empty(await da.ListAsync("select version from dblive.migration"));
		Assert.Empty(await da.ListAsync("select relative_path from dblive.code"));
	}

	[Fact]
	public async Task Code_file_that_breaks_its_connection_fails_the_deployment_with_its_own_error()
	{
		// the check of the transaction opens a connection, which cannot enlist in the transaction of the broken one.
		using TempProject project = CreateProject("raiserror('boom', 20, 1) with log;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith("Deploy code file error. File path: Code/a.sql. The error ended the deployment transaction, so the file is not retried." + Environment.NewLine, codeEx.Message);
		// the message goes on with "Process ID <n> has raised user error 50000, severity 20. SQL Server is terminating this process."
		Assert.StartsWith("boom", Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await da.ListAsync("select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Empty(await da.ListAsync("select name from sys.views"));
		Assert.Empty(await da.ListAsync("select version from dblive.migration"));
	}

	// also for the code file that outlives DeploymentTimeout, in TransactionTimeoutTests: they do not run in parallel with other tests.
	internal static TempProject CreateProject(string fileA, string settings = """{ "TransactionWrapLevel": "Deployment" }""")
	{
		return new TempProject()
			.AddFile("settings.json", settings)
			.AddFile("Migrations/001.migration.sql", "create table dbo.M ( x int );")
			.AddFile("Code/a.sql", fileA)
			// not b.sql: that is the name of a breaking script.
			.AddFile("Code/c.sql", "create view dbo.C as select 1 as x;");
	}
}
