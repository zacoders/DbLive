using DbLive.Adapter;
using DbLive.Schema;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SchemaSnapshotTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private const string Schema = """
		create extension if not exists pg_trgm;
		create schema app;
		create type app.mood as enum ('sad', 'ok', 'happy');
		create domain app.code as varchar(20) not null check (value <> '');
		create type app.pair as (a int, b text);
		create sequence app.seq as bigint start 10 increment 5;
		create table public.dept (id int generated always as identity (start with 100 increment by 2) primary key, name text not null unique);
		create table app.person (
			id serial primary key,
			name varchar(50) not null,
			phone text null default '',
			code app.code,
			price numeric(10, 2) not null default 0,
			total numeric generated always as (price * 2) stored,
			dept_id int references public.dept (id) on delete cascade,
			age int check (age > 0),
			email text collate "C",
			mood app.mood,
			constraint ck_person_name check (length(name) > 0),
			constraint uq_person_email unique (email)
		);
		create index ix_person_name on app.person (name, age desc) include (phone) where age is not null;
		create index ix_person_name_trgm on app.person using gin (name gin_trgm_ops);
		create view app.v_person as select id, name from app.person;
		create materialized view app.mv_person as select id, name from app.person;
		create function app.fn_double(x int) returns int language sql as $$ select x * 2 $$;
		create function app.fn_double(x numeric) returns numeric language sql as $$ select x * 2 $$;
		create procedure app.do_nothing() language plpgsql as $$ begin null; end $$;
		create function app.tr_fn() returns trigger language plpgsql as $$ begin return new; end $$;
		create trigger tr_person before insert on app.person for each row execute function app.tr_fn();
		alter table app.person disable trigger tr_person;
		create aggregate app.my_sum (int) (sfunc = int4pl, stype = int, initcond = '0');
		create table app.measure (id int, at date) partition by range (at);
		create table app.measure_2026 partition of app.measure for values from ('2026-01-01') to ('2027-01-01');
		create unlogged table app.cache (k text primary key, v text);
		alter table app.cache add constraint ex_cache exclude using btree (v with =);
		""";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	private IDbLiveInternalDA _da = null!;

	public async Task InitializeAsync()
	{
		_da = new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveInternalDA>();
		await _da.CreateDBAsync();
	}

	public Task DisposeAsync() => _da.DropDbAsync();

	[Fact]
	public async Task Snapshot_describes_each_kind_of_object()
	{
		await _da.ExecuteNonQueryAsync(Schema);

		SchemaSnapshot snapshot = await _da.GetSchemaSnapshotAsync();

		_output.WriteLine(string.Join(Environment.NewLine, snapshot.Objects.Select(o => $"{o.Type} {o.Name}: {o.Definition}")));

		Assert.Equal("", Definition(snapshot, SchemaObjectType.Schema, "app"));
		Assert.StartsWith("version ", Definition(snapshot, SchemaObjectType.Extension, "pg_trgm"));
		Assert.Equal("", Definition(snapshot, SchemaObjectType.Table, "app.person"));
		Assert.Equal("partition by RANGE (at)", Definition(snapshot, SchemaObjectType.Table, "app.measure"));
		Assert.Equal("partition of app.measure FOR VALUES FROM ('2026-01-01') TO ('2027-01-01')", Definition(snapshot, SchemaObjectType.Table, "app.measure_2026"));
		Assert.Equal("unlogged", Definition(snapshot, SchemaObjectType.Table, "app.cache"));
		Assert.Equal("integer generated always as identity (start 100 increment 2) not null", Definition(snapshot, SchemaObjectType.Column, "public.dept.id"));
		Assert.Equal("integer not null default nextval('app.person_id_seq'::regclass)", Definition(snapshot, SchemaObjectType.Column, "app.person.id"));
		Assert.Equal("text null default ''::text", Definition(snapshot, SchemaObjectType.Column, "app.person.phone"));
		Assert.Equal("app.code null", Definition(snapshot, SchemaObjectType.Column, "app.person.code"));
		Assert.Equal("numeric null generated always as ((price * (2)::numeric)) stored", Definition(snapshot, SchemaObjectType.Column, "app.person.total"));
		Assert.Equal("text collate \"C\" null", Definition(snapshot, SchemaObjectType.Column, "app.person.email"));
		Assert.Equal("constraint person_pkey PRIMARY KEY (id)", Definition(snapshot, SchemaObjectType.PrimaryKey, "app.person"));
		Assert.Equal("UNIQUE (name)", Definition(snapshot, SchemaObjectType.UniqueConstraint, "public.dept.dept_name_key"));
		Assert.Equal("FOREIGN KEY (dept_id) REFERENCES dept(id) ON DELETE CASCADE", Definition(snapshot, SchemaObjectType.ForeignKey, "app.person.person_dept_id_fkey"));
		Assert.Equal("CHECK ((age > 0))", Definition(snapshot, SchemaObjectType.CheckConstraint, "app.person.person_age_check"));
		Assert.Equal("EXCLUDE USING btree (v WITH =)", Definition(snapshot, SchemaObjectType.ExclusionConstraint, "app.cache.ex_cache"));
		Assert.Equal("CREATE INDEX ix_person_name ON app.person USING btree (name, age DESC) INCLUDE (phone) WHERE (age IS NOT NULL)", Definition(snapshot, SchemaObjectType.Index, "app.person.ix_person_name"));
		Assert.Equal("CREATE INDEX ix_person_name_trgm ON app.person USING gin (name gin_trgm_ops)", Definition(snapshot, SchemaObjectType.Index, "app.person.ix_person_name_trgm"));
		Assert.Equal("SELECT id, name FROM app.person;", Definition(snapshot, SchemaObjectType.View, "app.v_person"));
		Assert.Equal("SELECT id, name FROM app.person;", Definition(snapshot, SchemaObjectType.MaterializedView, "app.mv_person"));
		Assert.Equal("CREATE OR REPLACE FUNCTION app.fn_double(x integer) RETURNS integer LANGUAGE sql AS $function$ select x * 2 $function$", Definition(snapshot, SchemaObjectType.Function, "app.fn_double(x integer)"));
		Assert.Equal("CREATE OR REPLACE FUNCTION app.fn_double(x numeric) RETURNS numeric LANGUAGE sql AS $function$ select x * 2 $function$", Definition(snapshot, SchemaObjectType.Function, "app.fn_double(x numeric)"));
		Assert.Equal("CREATE OR REPLACE PROCEDURE app.do_nothing() LANGUAGE plpgsql AS $procedure$ begin null; end $procedure$", Definition(snapshot, SchemaObjectType.Procedure, "app.do_nothing()"));
		Assert.Equal("aggregate (sfunc = int4pl, stype = integer, initcond = 0)", Definition(snapshot, SchemaObjectType.Function, "app.my_sum(integer)"));
		Assert.Equal("CREATE TRIGGER tr_person BEFORE INSERT ON app.person FOR EACH ROW EXECUTE FUNCTION app.tr_fn() disabled", Definition(snapshot, SchemaObjectType.Trigger, "app.person.tr_person"));
		Assert.Equal("enum ('sad', 'ok', 'happy')", Definition(snapshot, SchemaObjectType.Type, "app.mood"));
		Assert.Equal("domain character varying(20) not null constraint code_check CHECK (((VALUE)::text <> ''::text))", Definition(snapshot, SchemaObjectType.Type, "app.code"));
		Assert.Equal("(a integer, b text)", Definition(snapshot, SchemaObjectType.Type, "app.pair"));
		Assert.Equal("bigint start 10 increment 5 minvalue 1 maxvalue 9223372036854775807 cache 1 no cycle", Definition(snapshot, SchemaObjectType.Sequence, "app.seq"));
		Assert.Equal("integer start 1 increment 1 minvalue 1 maxvalue 2147483647 cache 1 no cycle owned by app.person.id", Definition(snapshot, SchemaObjectType.Sequence, "app.person_id_seq"));

		// the objects of the extension and the sequence of the identity column are not listed.
		Assert.DoesNotContain(snapshot.Objects, o => o.Name.Contains("gtrgm") || o.Name.Contains("similarity"));
		Assert.DoesNotContain(snapshot.Objects, o => o.Name == "public.dept_id_seq");

		Assert.Equal("app.person", snapshot.Find(SchemaObjectType.Column, "app.person.id")!.Parent);
		Assert.Equal("app", snapshot.Find(SchemaObjectType.Table, "app.person")!.Parent);
	}

	[Fact]
	public async Task Recreated_objects_and_column_order_are_ignored()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		// the columns come back at the end of the table, PostgreSQL derives the same names for the constraints.
		await _da.ExecuteNonQueryAsync("""
			drop index app.ix_person_name;
			alter table app.person drop column age;
			alter table app.person add column age int check (age > 0);
			alter table app.person drop constraint person_dept_id_fkey;
			alter table app.person add foreign key (dept_id) references public.dept (id) on delete cascade;
			alter table app.person drop column phone;
			alter table app.person add column phone text default '';
			create index ix_person_name on app.person (name, age desc) include (phone) where age is not null;
			drop view app.v_person;
			create view app.v_person as
				select id,   name
				from app.person;
			create or replace function app.fn_double(x int) returns int language sql as $$
				select x * 2
			$$;
			""");

		AssertSame(before, await _da.GetSchemaSnapshotAsync());
	}

	[Fact]
	public async Task Current_identity_and_sequence_values_and_dblive_schema_are_ignored()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		await _da.ExecuteNonQueryAsync("""
			insert into public.dept (name) values ('a'), ('b');
			insert into app.person (name, code, price) values ('a', 'x', 1);
			select nextval('app.seq');
			create statistics st_person on name, age from app.person;
			analyze app.person;
			create schema dblive;
			create table dblive.t (id int);
			""");

		AssertSame(before, await _da.GetSchemaSnapshotAsync());
	}

	[Fact]
	public async Task Changed_objects_are_found()
	{
		await _da.ExecuteNonQueryAsync(Schema);
		SchemaSnapshot before = await _da.GetSchemaSnapshotAsync();

		await _da.ExecuteNonQueryAsync("""
			alter table app.person drop constraint ck_person_name;
			alter table app.person drop column email;
			create index ix_person_age on app.person (age);
			alter sequence app.seq increment 10;
			""");

		SchemaDiff diff = SchemaDiff.Compare(before, await _da.GetSchemaSnapshotAsync());

		Assert.Equal(
			"""
			  missing check constraint app.person.ck_person_name: CHECK ((length((name)::text) > 0))
			  missing column app.person.email: text collate "C" null
			  missing unique constraint app.person.uq_person_email: UNIQUE (email)
			  extra index app.person.ix_person_age: CREATE INDEX ix_person_age ON app.person USING btree (age)
			  changed sequence app.seq
			    was: bigint start 10 increment 5 minvalue 1 maxvalue 9223372036854775807 cache 1 no cycle
			    now: bigint start 10 increment 10 minvalue 1 maxvalue 9223372036854775807 cache 1 no cycle
			""".ReplaceLineEndings().TrimEnd(),
			diff.ToString()
		);
	}

	private static string Definition(SchemaSnapshot snapshot, SchemaObjectType type, string name)
		=> (snapshot.Find(type, name) ?? throw new Exception($"{type} {name} is not in the snapshot.")).Definition;

	private static void AssertSame(SchemaSnapshot before, SchemaSnapshot after)
	{
		SchemaDiff diff = SchemaDiff.Compare(before, after);
		Assert.True(diff.IsEmpty, diff.ToString());
	}
}
