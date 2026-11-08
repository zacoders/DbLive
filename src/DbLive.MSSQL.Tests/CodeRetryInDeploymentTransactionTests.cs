using DbLive.Adapter;
using DbLive.Deployers.Code;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// With <c>TransactionWrapLevel: Deployment</c> a failed code file is retried inside the deployment transaction. Some errors roll that
/// transaction back on SQL Server, and the next commands then run in autocommit mode: a retry and the files after it would be committed
/// while the migrations before them are rolled back. Such a file is not retried.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeRetryInDeploymentTransactionTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).CreateDbLiveDA().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Code_file_that_fails_on_a_missing_dependency_is_retried()
	{
		using TempProject project = CreateProject("create view dbo.A as select x from dbo.C;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.CreateDbLiveDA();
		Assert.Equal(["M"], await ListAsync(da, "select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Equal(["A", "C"], await ListAsync(da, "select name from sys.views"));
		Assert.Equal(["code/a.sql:applied", "code/c.sql:applied"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
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
		Assert.Equal("Deploy code file error. File path: Code/a.sql. The error ended the deployment transaction, so the file is not retried.", codeEx.Message);
		Assert.Equal(error, Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		// nothing is committed: neither the migration before the error nor the file after it.
		IDbLiveDA da = builder.CreateDbLiveDA();
		Assert.Empty(await ListAsync(da, "select name from sys.tables where schema_id = schema_id('dbo')"));
		Assert.Empty(await ListAsync(da, "select name from sys.views"));
		Assert.Empty(await ListAsync(da, "select version from dblive.migration"));
		Assert.Empty(await ListAsync(da, "select relative_path from dblive.code"));
	}

	private static TempProject CreateProject(string fileA)
	{
		return new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.M ( x int );")
			.AddFile("Code/a.sql", fileA)
			// not b.sql: that is the name of a breaking script.
			.AddFile("Code/c.sql", "create view dbo.C as select 1 as x;");
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
