using DbLive.Adapter;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The result sets of a SQL test hold the selected columns only. PostgreSqlDA reads with CommandBehavior.KeyInfo,
/// and Npgsql does not add key columns to the result set (unlike SqlClient on SQL Server).
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestResultSetTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output)
{
	private const string UsersTable = """
		create table users (
			user_id int not null primary key,
			name varchar(50) not null
		);
		""";

	private const string Arrange = "insert into users ( user_id, name ) values ( 10, 'TestUser10' );";

	[Fact]
	public async Task Result_set_of_a_select_without_the_key_column_has_no_hidden_columns()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();

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
		IDbLiveTester tester = builder.CreateTester();
		foreach (string test in new[] { "Tests/rows.sql", "Tests/rows-with-schema.sql" })
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.True(result.IsSuccess, result.ErrorMessage);
		}
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

		TestRunResult nan = await tester.RunTestAsync(Output.WriteLine, "Tests/nan.sql");
		TestRunResult nanAgainstNumber = await tester.RunTestAsync(Output.WriteLine, "Tests/nan-against-number.sql");

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
}
