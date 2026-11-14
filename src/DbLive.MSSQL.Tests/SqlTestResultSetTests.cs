using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The result sets of a SQL test hold the selected columns only. Read with CommandBehavior.KeyInfo, SqlClient turns on browse mode
/// (SET NO_BROWSETABLE ON), and SQL Server adds the key columns of the base table to the result set as hidden columns.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestResultSetTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string UsersTable = """
		create table dbo.Users (
			UserId int identity not null constraint PK_Users primary key
		  , Name nvarchar(50) not null
		);
		""";

	private const string GetUserName = """
		create or alter proc dbo.GetUserName
			@UserId int
		as
			select Name
			from dbo.Users
			where UserId = @UserId;
		""";

	private const string Arrange = """
		set identity_insert dbo.Users on;
		insert into dbo.Users ( UserId, Name ) values ( 10, 'TestUser10' );
		set identity_insert dbo.Users off;
		""";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveInternalDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Result_set_of_a_select_without_the_key_column_has_no_hidden_columns()
	{
		IDbLiveInternalDA da = new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveInternalDA>();

		await da.ExecuteNonQueryAsync(UsersTable);
		await da.ExecuteNonQueryAsync(GetUserName);
		await da.ExecuteNonQueryAsync(Arrange);

		List<SqlResult> results = await da.ExecuteQueryMultipleAsync("""
			select Name from dbo.Users where UserId = 10;
			exec dbo.GetUserName @UserId = 10;
			""");

		Assert.Equal(2, results.Count);
		Assert.All(results, result =>
		{
			Assert.Equal([new SqlColumn("Name", "nvarchar(50)")], result.Columns);
			Assert.Equal(["TestUser10"], Assert.Single(result.Rows));
		});
	}

	[Fact]
	public async Task Rows_tests_on_a_select_without_the_key_column_pass()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", UsersTable)
			.AddFile("Code/dbo/GetUserName.sql", GetUserName)
			.AddFile("Tests/rows.sql", $"""
				{Arrange}
				exec dbo.GetUserName @UserId = 10;

				select assert = 'rows';
				select 'TestUser10' as Name;
				""")
			.AddFile("Tests/rows-with-schema.sql", $"""
				{Arrange}
				exec dbo.GetUserName @UserId = 10;

				select assert = 'rows-with-schema';
				select cast('TestUser10' as nvarchar(50)) as Name;
				""");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateTester().RunAllTestsAsync();

		List<SqlResult> testRuns = await builder.GetRequiredService<IDbLiveInternalDA>().ExecuteQueryMultipleAsync(
			"select relative_path, pass from dblive.unit_test_run");

		Assert.Equal(
			["tests/rows-with-schema.sql:True", "tests/rows.sql:True"],
			testRuns.Single().Rows.Select(r => $"{r[0]}:{r[1]}").Order(StringComparer.Ordinal)
		);
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
