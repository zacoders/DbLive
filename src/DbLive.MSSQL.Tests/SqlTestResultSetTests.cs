using DbLive.Adapter;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The result sets of a SQL test hold the selected columns only. Read with CommandBehavior.KeyInfo, SqlClient turns on browse mode
/// (SET NO_BROWSETABLE ON), and SQL Server adds the key columns of the base table to the result set as hidden columns.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestResultSetTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
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

	[Fact]
	public async Task Result_set_of_a_select_without_the_key_column_has_no_hidden_columns()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();

		await da.ExecuteSqlAsync(UsersTable);
		await da.ExecuteSqlAsync(GetUserName);
		await da.ExecuteSqlAsync(Arrange);

		List<SqlResult> results = await da.QuerySqlAsync("""
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
		IDbLiveTester tester = builder.CreateTester();
		foreach (string test in new[] { "Tests/rows.sql", "Tests/rows-with-schema.sql" })
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.True(result.IsSuccess, result.ErrorMessage);
		}
	}

	[Fact]
	public async Task Column_types_are_named_as_in_TSql()
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();

		// no rows: reading a geography or hierarchyid value needs Microsoft.SqlServer.Types, and the names come from the schema.
		List<SqlResult> results = await da.QuerySqlAsync("""
			select cast('10:00' as time(3)) as t3
			     , cast('10:00' as time) as t7
			     , cast(N'x' as nvarchar(max)) as nm
			     , cast('x' as varchar(max)) as vm
			     , cast(0x01 as varbinary(max)) as bm
			     , geography::Point(1, 2, 4326) as geo
			     , hierarchyid::GetRoot() as h
			     , cast('{"a":1}' as json) as j
			     , cast('[1,2,3]' as vector(3)) as v
			where 1 = 0;
			""");

		Assert.Equal(
			[
				new SqlColumn("t3", "time(3)"),
				new SqlColumn("t7", "time(7)"),
				new SqlColumn("nm", "nvarchar(max)"),
				new SqlColumn("vm", "varchar(max)"),
				new SqlColumn("bm", "varbinary(max)"),
				new SqlColumn("geo", "geography"),
				new SqlColumn("h", "hierarchyid"),
				new SqlColumn("j", "json"),
				new SqlColumn("v", "vector")
			],
			Assert.Single(results).Columns
		);
	}

	[Fact]
	public async Task Rows_with_schema_test_fails_for_time_of_another_scale()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/time.sql", """
				select cast('10:00' as time(3)) as t, cast(N'x' as nvarchar(max)) as n;

				select assert = 'rows-with-schema';
				select cast('10:00' as time(7)) as t, cast(N'x' as nvarchar(10)) as n;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/time.sql");

		Assert.False(result.IsSuccess);
		Assert.Equal(
			"""
			Columns does not match:
			Expected columns: t: time(7), n: nvarchar(10)
			Actual columns:   t: time(3), n: nvarchar(max)
			""",
			result.ErrorMessage,
			ignoreLineEndingDifferences: true
		);
	}
}
