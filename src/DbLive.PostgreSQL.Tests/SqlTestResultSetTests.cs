using DbLive.Adapter;
using DbLive.Testing;
using Npgsql;
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
	/// Npgsql reads date as DateOnly and time as TimeOnly, so rows writes a date without a time of day: "2026-01-01", not
	/// "2026-01-01T00:00:00" as on SQL Server, where SqlClient reads date as DateTime. A date then equals the string '2026-01-01',
	/// and never a timestamp at midnight. An upgrade of Npgsql that reads them as other types fails this test.
	/// </summary>
	[Fact]
	public async Task Rows_test_writes_date_and_time_as_PostgreSQL_shows_them()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/date-and-time-against-text.sql", """
				select cast('2026-01-01' as date) as d, cast('10:00' as time(2)) as t;
				select 'rows' as assert;
				select '2026-01-01' as d, '10:00:00' as t;
				""")
			.AddFile("Tests/date-against-timestamp.sql", """
				select cast('2026-01-01' as date) as d;
				select 'rows' as assert;
				select cast('2026-01-01' as timestamp) as d;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult againstText = await tester.RunTestAsync(Output.WriteLine, "Tests/date-and-time-against-text.sql");
		TestRunResult againstTimestamp = await tester.RunTestAsync(Output.WriteLine, "Tests/date-against-timestamp.sql");

		Assert.True(againstText.IsSuccess, againstText.ErrorMessage);
		Assert.False(againstTimestamp.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: d: timestamp
			[Row 1]>
			Expected values: "2026-01-01T00:00:00"
			Actual values:   "2026-01-01"
			""".ReplaceLineEndings(),
			againstTimestamp.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// Npgsql gives the precision of timestamp, timestamptz, time, timetz and interval in NumericPrecision, not in NumericScale:
	/// rows-with-schema took timestamp(0) for timestamp (0096-153). Precision 6 is named without it: it is the largest one, and a type
	/// without a precision keeps microseconds too. PostgreSQL drops the precision of the columns a function returns, so a test of a
	/// function casts the expected values without it. An upgrade of Npgsql that gives the precision elsewhere fails this test.
	/// </summary>
	[Fact]
	public async Task Rows_with_schema_test_compares_the_precision_of_date_and_time_types()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create table events ( at timestamp(0) not null, starts time(2) not null );
				insert into events ( at, starts ) values ( '2026-01-01 10:00:00', '10:00' );
				create function get_events() returns table ( at timestamp(0), starts time(2) ) language sql
				as $$ select at, starts from events $$;
				""")
			.AddFile("Tests/table-against-no-precision.sql", """
				select at, starts from events;
				select 'rows-with-schema' as assert;
				select cast('2026-01-01 10:00:00' as timestamp) as at, cast('10:00' as time) as starts;
				""")
			.AddFile("Tests/table-against-same-precision.sql", """
				select at, starts from events;
				select 'rows-with-schema' as assert;
				select cast('2026-01-01 10:00:00' as timestamp(0)) as at, cast('10:00' as time(2)) as starts;
				""")
			.AddFile("Tests/function-against-no-precision.sql", """
				select * from get_events();
				select 'rows-with-schema' as assert;
				select cast('2026-01-01 10:00:00' as timestamp) as at, cast('10:00' as time) as starts;
				""")
			.AddFile("Tests/other-precision.sql", """
				select cast('2026-01-01 10:00:00+00' as timestamptz(3)) as tz, cast('10:00+02' as timetz(2)) as ttz
				     , cast('00:00:01' as interval(3)) as i;
				select 'rows-with-schema' as assert;
				select cast('2026-01-01 10:00:00+00' as timestamptz) as tz, cast('10:00+02' as timetz) as ttz
				     , cast('00:00:01' as interval) as i;
				""")
			.AddFile("Tests/precision-6-against-no-precision.sql", """
				select cast('2026-01-01 10:00:00' as timestamp(6)) as ts, cast('2026-01-01 10:00:00+00' as timestamptz(6)) as tz
				     , cast('10:00' as time(6)) as t, cast('10:00+02' as timetz(6)) as ttz, cast('01:02' as interval hour to minute) as i;
				select 'rows-with-schema' as assert;
				select cast('2026-01-01 10:00:00' as timestamp) as ts, cast('2026-01-01 10:00:00+00' as timestamptz) as tz
				     , cast('10:00' as time) as t, cast('10:00+02' as timetz) as ttz, cast('01:02' as interval) as i;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		foreach (string test in new[]
		{
			"Tests/table-against-same-precision.sql", "Tests/function-against-no-precision.sql", "Tests/precision-6-against-no-precision.sql"
		})
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.True(result.IsSuccess, $"{test}: {result.ErrorMessage}");
		}

		TestRunResult tableAgainstNoPrecision = await tester.RunTestAsync(Output.WriteLine, "Tests/table-against-no-precision.sql");
		Assert.False(tableAgainstNoPrecision.IsSuccess);
		Assert.Equal(
			"""
			Columns does not match:
			Expected columns: at: timestamp, starts: time
			Actual columns:   at: timestamp(0), starts: time(2)
			""".ReplaceLineEndings(),
			tableAgainstNoPrecision.ErrorMessage?.ReplaceLineEndings()
		);

		TestRunResult otherPrecision = await tester.RunTestAsync(Output.WriteLine, "Tests/other-precision.sql");
		Assert.False(otherPrecision.IsSuccess);
		Assert.Equal(
			"""
			Columns does not match:
			Expected columns: tz: timestamptz, ttz: timetz, i: interval
			Actual columns:   tz: timestamptz(3), ttz: timetz(2), i: interval(3)
			""".ReplaceLineEndings(),
			otherPrecision.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// An unbounded bpchar keeps the whole string and is not char(1); an unbounded varbit was named varbit() (0096-153).
	/// </summary>
	[Fact]
	public async Task Rows_with_schema_test_names_unbounded_bpchar_and_varbit()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/unbounded.sql", """
				select cast('a' as bpchar) as c, cast(B'101' as varbit) as b;
				select 'rows-with-schema' as assert;
				select cast('a' as char(1)) as c, cast(B'101' as varbit(5)) as b;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/unbounded.sql");

		Assert.False(result.IsSuccess);
		Assert.Equal(
			"""
			Columns does not match:
			Expected columns: c: char(1), b: varbit(5)
			Actual columns:   c: bpchar, b: varbit
			""".ReplaceLineEndings(),
			result.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// Npgsql reads macaddr as PhysicalAddress and pg_lsn as NpgsqlLogSequenceNumber, which have no public properties, and inet as
	/// IPAddress, which has no netmask: PostgreSqlDA reads inet as NpgsqlInet. rows compares them by their text.
	/// </summary>
	[Fact]
	public async Task Rows_test_compares_network_addresses_and_pg_lsn()
	{
		const string Values = """
			select '08:00:2b:01:02:03'::macaddr as m, '08:00:2b:01:02:03:04:05'::macaddr8 as m8
			     , '192.168.0.1'::inet as i, '192.168.0.1/24'::inet as n, '::1'::inet as i6, null::inet as ni, '192.168.0.0/24'::cidr as c
			     , '16/B374D848'::pg_lsn as l, array['10.0.0.1'::inet, '10.0.0.1/8', null] as a;
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
			.AddFile("Tests/other-netmask-in-array.sql", """
				select array['10.0.0.1/24'::inet] as a;
				select 'rows' as assert;
				select array['10.0.0.1/16'::inet] as a;
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

		foreach (string test in new[]
		{
			"Tests/other-macaddr.sql", "Tests/other-inet.sql", "Tests/other-netmask-in-array.sql", "Tests/other-cidr.sql", "Tests/other-pg-lsn.sql"
		})
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.False(result.IsSuccess, test);
			Assert.StartsWith("Data for one or more rows does not match:", result.ErrorMessage);
		}

		TestRunResult otherNetmask = await tester.RunTestAsync(Output.WriteLine, "Tests/other-netmask.sql");
		Assert.False(otherNetmask.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: n: inet
			[Row 1]>
			Expected values: "192.168.0.1/16"
			Actual values:   "192.168.0.1/24"
			""".ReplaceLineEndings(),
			otherNetmask.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// The JSON of NpgsqlTsQuery, NpgsqlTsVector and NpgsqlPath loses a part of the value: the operands of a tsquery operator and the
	/// elements of a tsquery[], the positions and weights of a tsvector, whether a path is open. PostgreSqlDA reads them as their text,
	/// also in an array (0096-154).
	/// </summary>
	[Fact]
	public async Task Rows_test_compares_text_search_values_and_paths()
	{
		const string Values = """
			select 'a & (b | !c)'::tsquery as q, 'a:*A <2> b'::tsquery as qp, null::tsquery as nq, array['a'::tsquery, 'b & c', null] as qa
			     , 'a:1A,2B b:3'::tsvector as v, ''::tsvector as ve, null::tsvector as nv, array['a:1'::tsvector, null] as va
			     , '((0,0),(1,1))'::path as p, '[(0.5,-1),(1e20,2)]'::path as po, null::path as np, array['((0,0),(1,1))'::path] as pa;
			""";

		using TempProject project = new TempProject()
			.AddFile("Tests/same.sql", $"""
				{Values}
				select 'rows-with-schema' as assert;
				{Values}
				""")
			.AddFile("Tests/other-tsquery.sql", """
				select 'a & b'::tsquery as q;
				select 'rows' as assert;
				select 'a & c'::tsquery as q;
				""")
			.AddFile("Tests/other-tsquery-operand.sql", """
				select 'a <-> (b | c)'::tsquery as q;
				select 'rows' as assert;
				select 'a <-> (b | d)'::tsquery as q;
				""")
			.AddFile("Tests/other-tsquery-in-array.sql", """
				select array['b'::tsquery] as qa;
				select 'rows' as assert;
				select array['c'::tsquery] as qa;
				""")
			.AddFile("Tests/other-tsvector-positions.sql", """
				select 'a:1 b:2'::tsvector as v;
				select 'rows' as assert;
				select 'a:1 b:3'::tsvector as v;
				""")
			.AddFile("Tests/other-tsvector-weights.sql", """
				select 'a:1A'::tsvector as v;
				select 'rows' as assert;
				select 'a:1B'::tsvector as v;
				""")
			.AddFile("Tests/other-tsvector-in-array.sql", """
				select array['a:1A'::tsvector] as va;
				select 'rows' as assert;
				select array['a:1B'::tsvector] as va;
				""")
			.AddFile("Tests/open-against-closed-path.sql", """
				select '((0,0),(1,1))'::path as p;
				select 'rows' as assert;
				select '[(0,0),(1,1)]'::path as p;
				""")
			.AddFile("Tests/open-against-closed-path-in-array.sql", """
				select array['((0,0),(1,1))'::path] as pa;
				select 'rows' as assert;
				select array['[(0,0),(1,1)]'::path] as pa;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult same = await tester.RunTestAsync(Output.WriteLine, "Tests/same.sql");
		Assert.True(same.IsSuccess, same.ErrorMessage);

		foreach (string test in new[]
		{
			"Tests/other-tsquery-operand.sql", "Tests/other-tsquery-in-array.sql", "Tests/other-tsvector-positions.sql",
			"Tests/other-tsvector-weights.sql", "Tests/other-tsvector-in-array.sql", "Tests/open-against-closed-path-in-array.sql"
		})
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.False(result.IsSuccess, test);
			Assert.StartsWith("Data for one or more rows does not match:", result.ErrorMessage);
		}

		TestRunResult otherTsQuery = await tester.RunTestAsync(Output.WriteLine, "Tests/other-tsquery.sql");
		Assert.False(otherTsQuery.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: q: tsquery
			[Row 1]>
			Expected values: "'a' & 'c'"
			Actual values:   "'a' & 'b'"
			""".ReplaceLineEndings(),
			otherTsQuery.ErrorMessage?.ReplaceLineEndings()
		);

		TestRunResult openAgainstClosed = await tester.RunTestAsync(Output.WriteLine, "Tests/open-against-closed-path.sql");
		Assert.False(openAgainstClosed.IsSuccess);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: p: path
			[Row 1]>
			Expected values: "[(0,0),(1,1)]"
			Actual values:   "((0,0),(1,1))"
			""".ReplaceLineEndings(),
			openAgainstClosed.ErrorMessage?.ReplaceLineEndings()
		);
	}

	/// <summary>
	/// DbLive reads every value of every result set, and Npgsql cannot read some of them: a composite type, regclass, a numeric NaN.
	/// The test failed with the error of Npgsql alone, which names neither the column nor what to do (0096-166, 0096-168).
	/// </summary>
	[Fact]
	public async Task Test_that_cannot_read_a_value_names_the_column_and_says_to_select_it_as_text()
	{
		Dictionary<string, (string Value, string Type)> unreadable = new()
		{
			["composite"] = ("p", "public.pair"),
			["composite-array"] = ("array[p]", "public.pair[]"),
			["composite-in-record"] = ("row(1, p)", "record"),
			["function"] = ("f_pair()", "public.pair"),
			["table-row"] = ("t", "-.-"),
			["regclass"] = ("'pg_class'::regclass", "regclass"),
			["pg-snapshot"] = ("'10:20:10,14'::pg_snapshot", "pg_snapshot"),
			["numeric-nan"] = ("'NaN'::numeric", "numeric"),
			["numeric-beyond-decimal"] = ("1e40::numeric", "numeric"),
			["interval-with-months"] = ("interval '1 month'", "interval"),
			["date-after-9999"] = ("'10000-01-01'::date", "date"),
		};

		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create type pair as (a int, b text);
				create table t (p pair);
				insert into t values (row(1, 'x'));
				create function f_pair() returns pair language sql as $$ select row(1, 'x')::pair $$;
				""")
			.AddFile("Tests/expected.sql", """
				select p::text as p from t;
				select 'rows' as assert;
				select row(1, 'x')::pair as p;
				""")
			.AddFile("Tests/as-text.sql", """
				select p::text as p, 'pg_class'::regclass::text as c from t;
				select 'rows-with-schema' as assert;
				select '(1,x)' as p, 'pg_class' as c;
				""")
			.AddFile("Tests/fields.sql", """
				select (p).* from t;
				select 'rows-with-schema' as assert;
				select 1 as a, 'x' as b;
				""")
			.AddFile("Tests/record.sql", """
				select row((p).*) as p from t;
				select 'rows-with-schema' as assert;
				select row(1, 'x') as p;
				""");
		foreach ((string name, (string value, _)) in unreadable)
		{
			_ = project.AddFile($"Tests/{name}.sql", $"select {value} as v from t;");
		}

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		IDbLiveTester tester = builder.CreateTester();

		TestRunResult composite = await tester.RunTestAsync(Output.WriteLine, "Tests/composite.sql");
		Assert.False(composite.IsSuccess);
		Assert.Equal(
			"""
			Cannot read the value of the column 'v' in row 1 of result set 1, of the type public.pair: Reading as 'System.Object' is not supported for fields having DataTypeName 'public.pair'
			Select the value as text in the test.
			""".ReplaceLineEndings(),
			composite.ErrorMessage?.ReplaceLineEndings()
		);

		foreach ((string name, (_, string type)) in unreadable)
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, $"Tests/{name}.sql");
			Assert.False(result.IsSuccess, name);
			Assert.StartsWith($"Cannot read the value of the column 'v' in row 1 of result set 1, of the type {type}: ", result.ErrorMessage);
			Assert.EndsWith("Select the value as text in the test.", result.ErrorMessage?.ReplaceLineEndings());
		}

		// the result sets of the script are counted, the assert result set too.
		TestRunResult expected = await tester.RunTestAsync(Output.WriteLine, "Tests/expected.sql");
		Assert.False(expected.IsSuccess);
		Assert.StartsWith("Cannot read the value of the column 'p' in row 1 of result set 3, of the type public.pair: ", expected.ErrorMessage);

		foreach (string test in new[] { "Tests/as-text.sql", "Tests/fields.sql", "Tests/record.sql" })
		{
			TestRunResult result = await tester.RunTestAsync(Output.WriteLine, test);
			Assert.True(result.IsSuccess, $"{test}: {result.ErrorMessage}");
		}
	}

	/// <summary>
	/// Npgsql loads the types of the database once for a connection string, when its first connection opens: in a deployment before the
	/// migrations. Without the reload at the end of the deployment the SQL tests in the process of the deployment failed on an enum, a
	/// domain and an array of an enum that a migration created, with KeyNotFoundException or InvalidCastException, and so did a read of
	/// the application with the same connection string (0096-165).
	/// </summary>
	[Fact]
	public async Task Tests_in_the_process_of_the_deployment_read_the_types_it_created()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", """
				create type mood as enum ('sad', 'happy');
				create domain ts0 as timestamp(0);
				create table diary (m mood not null, at ts0 not null, past mood[] not null);
				insert into diary values ('happy', '2026-01-01 10:00:00', array['sad', 'happy']::mood[]);
				""")
			.AddFile("Tests/diary.sql", """
				select m, at, past from diary;
				select 'rows' as assert;
				select cast('happy' as mood) as m, cast('2026-01-01 10:00:00' as timestamp) as at, array['sad', 'happy']::mood[] as past;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/diary.sql");
		Assert.True(result.IsSuccess, result.ErrorMessage);

		await using NpgsqlConnection cnn = new(DbCnnString);
		await cnn.OpenAsync();
		await using NpgsqlCommand read = new("select m from diary;", cnn);
		Assert.Equal("happy", await read.ExecuteScalarAsync());
	}
}
