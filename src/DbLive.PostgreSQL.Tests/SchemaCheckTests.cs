using DbLive.Testing;
using DbLive.xunit;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The schema check of the deployment tests: a second breaking deployment does not change the schema.
/// A migration breaking script runs once.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SchemaCheckTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	private const string CreatePerson = """
		create table person (
			id int not null primary key,
			name varchar(50) not null,
			phone varchar(20) null,
			constraint ck_person_name check (name <> '')
		);
		create index ix_person_name on person (name);
		""";

	// the migration drops an index and a constraint and adds a column, the breaking script drops the old column.
	private const string Migration = """
		drop index if exists ix_person_name;
		alter table person drop constraint if exists ck_person_name;
		alter table person add column if not exists email varchar(100) null;
		""";
	// without 'if exists': the deployment test does not run a migration breaking script a second time.
	private const string Breaking = "alter table person drop column phone;";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	[Fact]
	public async Task Breaking_script_runs_once()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking);

		await DeploymentTestAsync(project);
	}

	[Fact]
	public async Task Code_breaking_script_whose_second_run_drops_more_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "create or replace function get_user_v1() returns int language sql as $$ select 1 $$;")
			.AddFile("Code/public/get_user.sql", "create or replace function get_user_v2(p_user_id int) returns int language sql as $$ select p_user_id $$;")
			.AddFile("Code/public/get_user.breaking.sql", """
				do $$ begin
					if not exists (select 1 from pg_proc where proname = 'get_user_v1') then
						alter table person drop column if exists phone;
					end if;
				end $$;
				drop function if exists get_user_v1();
				""");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeploymentTestAsync(project)
		);

		Assert.EndsWith("  missing column public.person.phone: character varying(20) null", ex.Message);
	}

	[Fact]
	public async Task Idempotent_code_breaking_script_passes()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "create or replace function get_user_v1() returns int language sql as $$ select 1 $$;")
			.AddFile("Code/public/get_user.sql", "create or replace function get_user_v2(p_user_id int) returns int language sql as $$ select p_user_id $$;")
			.AddFile("Code/public/get_user.breaking.sql", "drop function if exists get_user_v1();");

		await DeploymentTestAsync(project);
	}

	private Task DeploymentTestAsync(TempProject project) =>
		new DeploymentTest(new FixtureBuilder(CreateBuilder(project)), dropDatabaseOnComplete: true).DeployAsync(_output);

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private sealed class FixtureBuilder(DbLiveBuilder builder) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() => Task.FromResult(builder);

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
