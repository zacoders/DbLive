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
		Assert.False(nanAgainstNumber.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: v: double precision
			[Row 1]>
			Expected values: 1
			Actual values:   "NaN"
			""".ReplaceLineEndings(),
			nanAgainstNumber.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// Npgsql reads macaddr as PhysicalAddress and pg_lsn as NpgsqlLogSequenceNumber, which have no public properties, and inet as
	/// IPAddress, whose getters throw. rows compares them by their text.
	/// </summary>
	[Fact]
	public async Task Rows_test_compares_network_addresses_and_pg_lsn()
	{
		const string Values = """
			select '08:00:2b:01:02:03'::macaddr as m, '08:00:2b:01:02:03:04:05'::macaddr8 as m8
			     , '192.168.0.1'::inet as i, '192.168.0.1/24'::inet as n, '::1'::inet as i6, '192.168.0.0/24'::cidr as c
			     , '16/B374D848'::pg_lsn as l, array['10.0.0.1'::inet] as a;
			""";

		using TempProject project = new TempProject()
			.AddFile("Tests/same.sql", $"""
				{Values}
				select 'rows-with-schema' as assert;
				{Values}
				""")
			.AddFile("Tests/null-against-macaddr.sql", """
				select null::macaddr as m;
				select 'rows' as assert;
				select '08:00:2b:01:02:03'::macaddr as m;
				""")
			.AddFile("Tests/other-macaddr.sql", """
				select '08:00:2b:01:02:03'::macaddr as m;
				select 'rows' as assert;
				select '08:00:2b:01:02:04'::macaddr as m;
				""")
			.AddFile("Tests/other-inet.sql", """
				select '192.168.0.1'::inet as i;
				select 'rows' as assert;
				select '192.168.0.2'::inet as i;
				""")
			.AddFile("Tests/other-netmask.sql", """
				select '192.168.0.1/24'::inet as n;
				select 'rows' as assert;
				select '192.168.0.1/16'::inet as n;
				""")
			.AddFile("Tests/other-cidr.sql", """
				select '192.168.0.0/24'::cidr as c;
				select 'rows' as assert;
				select '192.168.0.0/16'::cidr as c;
				""")
			.AddFile("Tests/other-pg-lsn.sql", """
				select '16/B374D848'::pg_lsn as l;
				select 'rows' as assert;
				select '17/B374D848'::pg_lsn as l;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult same = await tester.RunTestAsync(Output.WriteLine, "Tests/same.sql");
		Assert.True(same.IsSuccess, same.ErrorMessage);

		TestRunResult nullAgainstMacaddr = await tester.RunTestAsync(Output.WriteLine, "Tests/null-against-macaddr.sql");
		Assert.False(nullAgainstMacaddr.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: m: macaddr
			[Row 1]>
			Expected values: "08002B010203"
			Actual values:   null
			""".ReplaceLineEndings(),
			nullAgainstMacaddr.ErrorMessage?.ReplaceLineEndings()
		);

		foreach (string test in new[] { "Tests/other-macaddr.sql", "Tests/other-inet.sql", "Tests/other-cidr.sql", "Tests/other-pg-lsn.sql" })
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.False(result.IsSuccess, test);
			Assert.StartsWith("Data for one or more rows does not match:", result.ErrorMessage);
		}

		// Npgsql reads inet as IPAddress, without the netmask, so two inet values that differ only by it are equal (0096-145).
		TestRunResult otherNetmask = await tester.RunTestAsync(Output.WriteLine, "Tests/other-netmask.sql");
		Assert.True(otherNetmask.IsSuccess, otherNetmask.ErrorMessage);
	}
}
