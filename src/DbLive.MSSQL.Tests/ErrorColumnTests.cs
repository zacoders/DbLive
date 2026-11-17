using DbLive.Adapter;
using DbLive.Deployers.Code;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// The column <c>error</c> of the dblive tables is <c>nvarchar(4000)</c> on SQL Server. A failed code file stores the error of the server
/// without stack traces, and a longer text is cut to fit: otherwise the write of the row fails and its error replaces the one being recorded.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ErrorColumnTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	// SQL Server counts the line of an error from the start of its GO batch: the line above the errors names the line of the file
	// where the batch starts, also in a file of one batch.
	[Fact]
	public async Task Failed_code_file_stores_the_error_of_the_server_with_the_line_where_its_batch_starts()
	{
		Assert.Equal(
			"""
			SQL Server error:
			The batch starts at line 1 of the file.
			Msg 102, Level 15, State 1, Line 3
			Incorrect syntax near 'selec'.
			Procedure: GetUser
			""".ReplaceLineEndings(),
			await DeployFailingCodeFileAsync("create or alter proc dbo.GetUser\nas\nselec 1;")
		);
	}

	// the error is on line 5 of the file: the start of the batch plus its line in the batch, minus 1.
	[Fact]
	public async Task Error_in_the_second_batch_names_the_line_where_the_batch_starts()
	{
		Assert.Equal(
			"""
			SQL Server error:
			The batch starts at line 3 of the file.
			Msg 8134, Level 16, State 1, Line 3
			Divide by zero error encountered.
			""".ReplaceLineEndings(),
			await DeployFailingCodeFileAsync("create or alter view dbo.ErrorLine as select 1 as a;\nGO\n\nselect 1;\nselect 1 / 0;")
		);
	}

	private async Task<string> DeployFailingCodeFileAsync(string script)
	{
		using TempProject project = new TempProject().AddFile("Code/dbo/ErrorLine.sql", script);

		DbLiveBuilder builder = CreateBuilder(project);

		_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => builder.CreateDeployer().DeployAsync(DeployParameters.Default));

		return (await ErrorAsync(builder, "select error from dblive.code")).ReplaceLineEndings();
	}

	[Fact]
	public async Task Failed_code_file_with_an_error_longer_than_the_column_fails_with_its_own_error()
	{
		// every missing column is an error of its own: the error of the server is about 7000 characters.
		string columns = string.Join(", ", Enumerable.Range(1, 100).Select(i => $"missing_column_{i}"));
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/ManyErrors.sql", $"create or alter view dbo.ManyErrors as select {columns} from ( select 1 as a ) t;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Contains("Code/dbo/ManyErrors.sql", ex.Message);

		string error = await ErrorAsync(builder, "select error from dblive.code");
		Assert.Equal(MsSqlDA.ErrorColumnLength, error.Length);
		Assert.StartsWith("SQL Server error:", error);
		Assert.Contains("Invalid column name 'missing_column_1'.", error);
		Assert.EndsWith(MsSqlDA.TruncatedMark, error);
	}

	[Fact]
	public async Task Failed_test_with_an_error_longer_than_the_column_fails_the_run_with_UnitTestsFailedException()
	{
		// the message of the comparison holds the expected and the actual row: 4200 characters of values.
		using TempProject project = new TempProject()
			.AddFile("Tests/wide.sql", """
				select replicate(cast('x' as nvarchar(max)), 2100) as v;
				select assert = 'rows';
				select replicate(cast('y' as nvarchar(max)), 2100) as v;
				""");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(() => builder.CreateTester().RunAllTestsAsync());

		Assert.Equal("Tests/wide.sql", Assert.Single(ex.FailedTests));

		string error = await ErrorAsync(builder, "select error from dblive.unit_test_run");
		Assert.Equal(MsSqlDA.ErrorColumnLength, error.Length);
		Assert.StartsWith("Data for one or more rows does not match:", error);
		Assert.EndsWith(MsSqlDA.TruncatedMark, error);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<string> ErrorAsync(DbLiveBuilder builder, string query)
	{
		List<SqlResult> results = await builder.GetRequiredService<IDbLiveDA>().QuerySqlAsync(query);
		return (string)results.Single().Rows.Single()[0]!;
	}
}
