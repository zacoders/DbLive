using DbLive.Adapter;
using DbLive.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The result sets of a SQL test hold the selected columns only. PostgreSqlDA reads with CommandBehavior.KeyInfo,
/// and Npgsql does not add key columns to the result set (unlike SqlClient on SQL Server).
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestResultSetTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private const string UsersTable = """
		create table users (
			user_id int not null primary key,
			name varchar(50) not null
		);
		""";

	private const string Arrange = "insert into users ( user_id, name ) values ( 10, 'TestUser10' );";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Result_set_of_a_select_without_the_key_column_has_no_hidden_columns()
	{
		IDbLiveDA da = new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>();

		await da.ExecuteSqlAsync(UsersTable);
		await da.ExecuteSqlAsync(Arrange);

		List<SqlResult> results = await da.QuerySqlAsync("select name from users where user_id = 10;");

		SqlResult result = Assert.Single(results);
		Assert.Equal([new SqlColumn("name", "varchar(50)")], result.Columns);
		Assert.Equal(["TestUser10"], Assert.Single(result.Rows));
	}

	[Fact]
	public async Task Rows_tests_on_a_select_without_the_key_column_pass()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", UsersTable)
			.AddFile("Tests/rows.sql", $"""
				{Arrange}
				select name from users where user_id = 10;

				select 'rows' as assert;
				select 'TestUser10' as name;
				""")
			.AddFile("Tests/rows-with-schema.sql", $"""
				{Arrange}
				select name from users where user_id = 10;

				select 'rows-with-schema' as assert;
				select cast('TestUser10' as varchar(50)) as name;
				""");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateTester().RunAllTestsAsync();

		List<SqlResult> testRuns = await builder.GetRequiredService<IDbLiveDA>().QuerySqlAsync(
			"select relative_path, pass from dblive.unit_test_run");

		Assert.Equal(
			["tests/rows-with-schema.sql:True", "tests/rows.sql:True"],
			testRuns.Single().Rows.Select(r => $"{r[0]}:{r[1]}").Order(StringComparer.Ordinal)
		);
	}

	[Fact]
	public async Task Rows_test_compares_NaN_and_infinity()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/nan.sql", """
				select 'NaN'::float8 as v, '-Infinity'::float4 as i, array['NaN'::float8, 'Infinity'::float8] as a;

				select 'rows-with-schema' as assert;
				select 'NaN'::float8 as v, '-Infinity'::float4 as i, array['NaN'::float8, 'Infinity'::float8] as a;
				""")
			.AddFile("Tests/nan-against-number.sql", """
				select 'NaN'::float8 as v;

				select 'rows' as assert;
				select 1::float8 as v;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult nan = await tester.RunTestAsync(_output.WriteLine, "Tests/nan.sql");
		TestRunResult nanAgainstNumber = await tester.RunTestAsync(_output.WriteLine, "Tests/nan-against-number.sql");

		Assert.True(nan.IsSuccess, nan.ErrorMessage);
		// the values in the message are written in the current culture: NaN is "не число" in ru-RU.
		Assert.False(nanAgainstNumber.IsSuccess);
		Assert.StartsWith(
			"""
			Data for one or more rows does not match:
			Columns: v: double precision
			[Row 1]>
			Expected values: 1
			""",
			nanAgainstNumber.ErrorMessage?.ReplaceLineEndings("\n")
		);
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
