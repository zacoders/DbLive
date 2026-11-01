using DbLive.Schema;
using DbLive.xunit;
using DbLive.xunit.Deploy;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The schema checks of the deployment tests: an undo restores the schema, a breaking script does not change it on a second run.
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
	private const string Breaking = "alter table person drop column if exists phone;";
	private const string Undo = """
		alter table person add column if not exists phone varchar(20) null;
		alter table person drop column if exists email;
		do $$ begin
			if not exists (select 1 from pg_constraint where conname = 'ck_person_name') then
				alter table person add constraint ck_person_name check (name <> '');
			end if;
		end $$;
		create index if not exists ix_person_name on person (name);
		""";
	private const string UndoWithoutRestore = "alter table person drop column if exists email;";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	[Theory]
	[InlineData(UndoTestMode.MigrationUndoMigration)]
	[InlineData(UndoTestMode.MigrationBreakingUndoMigration)]
	public async Task Undo_that_restores_the_schema_passes(UndoTestMode undoTestMode)
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", Undo);

		await DeployAsync(project, undoTestMode);
	}

	[Fact]
	public async Task Undo_that_does_not_restore_a_column_an_index_and_a_constraint_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", UndoWithoutRestore);

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.MigrationBreakingUndoMigration)
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.Equal(
			"""
			Undo test: Migrations/002.undo.sql does not restore the schema. The schema after the undo differs from the schema before Migrations/002.migration.sql:
			  missing check constraint public.person.ck_person_name: CHECK (((name)::text <> ''::text))
			  missing index public.person.ix_person_name: CREATE INDEX ix_person_name ON public.person USING btree (name)
			  missing column public.person.phone: character varying(20) null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);
	}

	[Theory]
	[InlineData(Undo, false)]
	[InlineData(UndoWithoutRestore, true)]
	public async Task Checks_read_the_schema_inside_the_deployment_transaction(string undo, bool fails)
	{
		// the snapshot joins the deployment transaction: it sees the uncommitted schema and does not wait for its locks.
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionWrapLevel": "Deployment" }""")
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", undo);

		Exception? ex = await Record.ExceptionAsync(() => DeployAsync(project, UndoTestMode.MigrationBreakingUndoMigration));

		if (fails)
		{
			Assert.Equal(SchemaCheck.Undo, Assert.IsType<SchemaChangedException>(ex).Check);
		}
		else
		{
			Assert.Null(ex);
		}
	}

	[Fact]
	public async Task Undo_that_leaves_a_column_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "alter table person add column if not exists email varchar(100) null;")
			.AddFile("Migrations/002.undo.sql", "select 1;");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.MigrationUndoMigration)
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.EndsWith("  extra column public.person.email: character varying(100) null", ex.Message);
	}

	[Fact]
	public async Task Breaking_script_whose_second_run_drops_more_fails()
	{
		// the condition is checked before the trigger is dropped: the first run drops the trigger, the second one the column.
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", """
				create or replace function tmp_sync() returns trigger language plpgsql as $$ begin return new; end $$;
				create trigger tmp_sync after insert on person for each row execute function tmp_sync();
				""")
			.AddFile("Migrations/002.breaking.sql", """
				do $$ begin
					if not exists (select 1 from pg_trigger where tgname = 'tmp_sync') then
						alter table person drop column if exists phone;
					end if;
				end $$;
				drop trigger if exists tmp_sync on person;
				drop function if exists tmp_sync();
				""")
			.AddFile("Migrations/002.undo.sql", """
				drop trigger if exists tmp_sync on person;
				drop function if exists tmp_sync();
				alter table person add column if not exists phone varchar(20) null;
				""");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.MigrationBreakingUndoMigration)
		);

		Assert.Equal(SchemaCheck.BreakingSecondRun, ex.Check);
		Assert.Equal(
			"""
			Undo test: the breaking script Migrations/002.breaking.sql is not idempotent, its second run changed the schema:
			  missing column public.person.phone: character varying(20) null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);
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
			() => DeployWithFixtureAsync(project)
		);

		Assert.Equal(SchemaCheck.BreakingSecondDeployment, ex.Check);
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

		await DeployWithFixtureAsync(project);
	}

	private async Task DeployAsync(TempProject project, UndoTestMode undoTestMode)
	{
		DbLiveBuilder builder = CreateBuilder(project).ConfigureServices(s => s.AddXUnitLogger(_output));

		try
		{
			await builder.CreateDeployer().DeployAsync(new DeployParameters
			{
				DeployBreaking = true,
				UndoTestDeployment = undoTestMode
			});
		}
		finally
		{
			await builder.CreateDbLiveDA().DropDbAsync();
		}
	}

	private async Task DeployWithFixtureAsync(TempProject project)
	{
		DeployFixture fixture = new(new FixtureBuilder(CreateBuilder(project)), dropDatabaseOnComplete: true);
		await fixture.InitializeAsync();

		try
		{
			await fixture.DeployAsync(_output, deployBreaking: true, UndoTestMode.None);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

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
