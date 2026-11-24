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

	/// <summary>
	/// SqlClient keeps no types of the database, so a type that the deployment created needs no reload (0096-165, as on PostgreSQL):
	/// SQL Server sends an alias type as its base type.
	/// </summary>
	[Fact]
	public async Task Tests_in_the_process_of_the_deployment_read_the_types_it_created()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create type dbo.Code from nvarchar(20) not null;
				go
				create table dbo.Item ( Code dbo.Code );
				insert into dbo.Item ( Code ) values ( N'A1' );
				""")
			.AddFile("Tests/item.sql", """
				select Code from dbo.Item;

				select assert = 'rows-with-schema';
				select cast(N'A1' as nvarchar(20)) as Code;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/item.sql");
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}

	/// <summary>
	/// DbLive reads every value of every result set, and SqlClient cannot read some of them: a CLR type whose assembly the process cannot
	/// load (this test project has no Microsoft.SqlServer.Types), a decimal beyond the .NET decimal. The test failed with the error of
	/// SqlClient alone, which names neither the column nor what to do (0096-170, as 0096-166 on PostgreSQL).
	/// </summary>
	[Fact]
	public async Task Test_that_cannot_read_a_value_names_the_column_and_says_to_select_it_as_text()
	{
		const string BeyondDecimal = "cast(99999999999999999999999999999999999999 as decimal(38, 0))";
		const string BeyondDecimalScale = "cast(0.12345678901234567890123456789012345678 as decimal(38, 38))";

		Dictionary<string, (string Value, string Type)> unreadable = new()
		{
			["geography"] = ("geography::Point(1, 2, 4326)", "geography"),
			["geometry"] = ("geometry::Point(1, 2, 0)", "geometry"),
			["hierarchyid"] = ("hierarchyid::GetRoot()", "hierarchyid"),
			["decimal-beyond-decimal"] = (BeyondDecimal, "decimal(38, 0)"),
			["decimal-beyond-decimal-scale"] = (BeyondDecimalScale, "decimal(38, 38)"),
			["decimal-beyond-decimal-in-sql-variant"] = ($"cast({BeyondDecimal} as sql_variant)", "sql_variant"),
		};

		using TempProject project = new TempProject()
			.AddFile("Tests/second-row.sql", $"""
				select v from ( values ( cast(1 as decimal(38, 0)) ), ( {BeyondDecimal} ) ) t ( v );
				""")
			.AddFile("Tests/expected.sql", """
				select hierarchyid::GetRoot().ToString() as h;

				select assert = 'rows';
				select hierarchyid::GetRoot() as h;
				""")
			.AddFile("Tests/as-text.sql", $"""
				select geography::Point(1, 2, 4326).ToString() as geo
				     , geometry::Point(1, 2, 0).ToString() as geom
				     , hierarchyid::GetRoot().ToString() as h
				     , cast({BeyondDecimal} as varchar(40)) as d
				     , cast({BeyondDecimalScale} as varchar(40)) as ds;

				select assert = 'rows';
				select 'POINT (2 1)' as geo
				     , 'POINT (1 2)' as geom
				     , '/' as h
				     , '99999999999999999999999999999999999999' as d
				     , '0.12345678901234567890123456789012345678' as ds;
				""");
		foreach ((string name, (string value, _)) in unreadable)
		{
			_ = project.AddFile($"Tests/{name}.sql", $"select {value} as v;");
		}

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult second = await tester.RunTestAsync(Output.WriteLine, "Tests/second-row.sql");
		Assert.False(second.IsSuccess);
		Assert.Equal(
			"""
			Cannot read the value of the column 'v' in row 2 of result set 1, of the type decimal(38, 0): Conversion overflows.
			Select the value as text in the test.
			""".ReplaceLineEndings(),
			second.ErrorMessage?.ReplaceLineEndings()
		);

		foreach ((string name, (_, string type)) in unreadable)
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, $"Tests/{name}.sql");
			Assert.False(result.IsSuccess, name);
			Assert.StartsWith($"Cannot read the value of the column 'v' in row 1 of result set 1, of the type {type}: ", result.ErrorMessage);
			Assert.EndsWith("Select the value as text in the test.", result.ErrorMessage?.ReplaceLineEndings());
		}

		// the error of SqlClient names the assembly to add.
		TestRunResult geography = await tester.RunTestAsync(Output.WriteLine, "Tests/geography.sql");
		Assert.Contains(": Could not load file or assembly 'Microsoft.SqlServer.Types, ", geography.ErrorMessage);

		// the result sets of the script are counted, the assert result set too.
		TestRunResult expected = await tester.RunTestAsync(Output.WriteLine, "Tests/expected.sql");
		Assert.False(expected.IsSuccess);
		Assert.StartsWith("Cannot read the value of the column 'h' in row 1 of result set 3, of the type hierarchyid: ", expected.ErrorMessage);

		TestRunResult asText = await tester.RunTestAsync(Output.WriteLine, "Tests/as-text.sql");
		Assert.True(asText.IsSuccess, asText.ErrorMessage);
	}
}
