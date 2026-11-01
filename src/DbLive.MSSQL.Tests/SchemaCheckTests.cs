using DbLive.Schema;
using DbLive.xunit.Deploy;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The schema checks of the deployment tests: an undo restores the schema, a breaking script does not change it on a second run.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SchemaCheckTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private const string CreatePerson = """
		create table dbo.Person (
			Id int not null primary key,
			Name nvarchar(50) not null,
			Phone varchar(20) null,
			constraint CK_Person_Name check (Name <> '')
		);
		create index IX_Person_Name on dbo.Person (Name);
		""";

	// the migration drops an index and a constraint and adds a column, the breaking script drops the old column.
	private const string Migration = """
		drop index if exists IX_Person_Name on dbo.Person;
		alter table dbo.Person drop constraint if exists CK_Person_Name;
		if col_length('dbo.Person', 'Email') is null alter table dbo.Person add Email nvarchar(100) null;
		""";
	private const string Breaking = "alter table dbo.Person drop column if exists Phone;";
	private const string Undo = """
		if col_length('dbo.Person', 'Phone') is null alter table dbo.Person add Phone varchar(20) null;
		alter table dbo.Person drop column if exists Email;
		if object_id('dbo.CK_Person_Name') is null alter table dbo.Person add constraint CK_Person_Name check (Name <> '');
		if indexproperty(object_id('dbo.Person'), 'IX_Person_Name', 'IndexID') is null create index IX_Person_Name on dbo.Person (Name);
		""";
	private const string UndoWithoutPhone = """
		alter table dbo.Person drop column if exists Email;
		if object_id('dbo.CK_Person_Name') is null alter table dbo.Person add constraint CK_Person_Name check (Name <> '');
		if indexproperty(object_id('dbo.Person'), 'IX_Person_Name', 'IndexID') is null create index IX_Person_Name on dbo.Person (Name);
		""";
	private const string UndoWithoutRestore = "alter table dbo.Person drop column if exists Email;";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	[Fact]
	public async Task Undo_that_restores_the_schema_passes()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", Undo);

		await DeployAsync(project, UndoTestMode.UndoCycle);
	}

	[Fact]
	public async Task Undo_that_does_not_restore_an_index_and_a_constraint_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", UndoWithoutRestore);

		// the undo before the breaking script fails.
		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.UndoCycle)
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.Equal(
			"""
			Undo test: Migrations/002.undo.sql does not restore the schema. The schema after the undo differs from the schema before Migrations/002.migration.sql:
			  missing check constraint dbo.Person.CK_Person_Name: check ([Name]<>'')
			  missing index dbo.Person.IX_Person_Name: nonclustered (Name asc)
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);
	}

	[Fact]
	public async Task Undo_that_does_not_restore_a_column_dropped_by_the_breaking_script_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking)
			.AddFile("Migrations/002.undo.sql", UndoWithoutPhone);

		// the undo before the breaking script passes, the one after it fails.
		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.UndoCycle)
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.Equal(
			"""
			Undo test: Migrations/002.undo.sql does not restore the schema after the breaking script Migrations/002.breaking.sql. The schema after the undo differs from the schema before Migrations/002.migration.sql:
			  missing column dbo.Person.Phone: varchar(20) collate SQL_Latin1_General_CP1_CI_AS null
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

		Exception? ex = await Record.ExceptionAsync(() => DeployAsync(project, UndoTestMode.UndoCycle));

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
			.AddFile("Migrations/002.migration.sql", "if col_length('dbo.Person', 'Email') is null alter table dbo.Person add Email nvarchar(100) null;")
			.AddFile("Migrations/002.undo.sql", "select 1;");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.UndoCycle)
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.EndsWith("  extra column dbo.Person.Email: nvarchar(100) collate SQL_Latin1_General_CP1_CI_AS null", ex.Message);
	}

	[Fact]
	public async Task Breaking_script_whose_second_run_drops_more_fails()
	{
		// the condition is checked before the trigger is dropped: the first run drops the trigger, the second one the column.
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "create or alter trigger dbo.TMP_sync on dbo.Person after insert as set nocount on;")
			.AddFile("Migrations/002.breaking.sql", """
				if object_id('dbo.TMP_sync') is null
					alter table dbo.Person drop column if exists Phone;
				drop trigger if exists dbo.TMP_sync;
				""")
			.AddFile("Migrations/002.undo.sql", """
				drop trigger if exists dbo.TMP_sync;
				if col_length('dbo.Person', 'Phone') is null alter table dbo.Person add Phone varchar(20) null;
				""");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployAsync(project, UndoTestMode.UndoCycle)
		);

		Assert.Equal(SchemaCheck.BreakingSecondRun, ex.Check);
		Assert.Equal(
			"""
			Undo test: the breaking script Migrations/002.breaking.sql is not idempotent, its second run changed the schema:
			  missing column dbo.Person.Phone: varchar(20) collate SQL_Latin1_General_CP1_CI_AS null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);
	}

	[Fact]
	public async Task Code_breaking_script_whose_second_run_drops_more_fails()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "create or alter proc dbo.GetUser_v1 as select 1 as user_id;")
			.AddFile("Code/dbo/GetUser.sql", "create or alter proc dbo.GetUser_v2 @UserId int as select @UserId as user_id;")
			.AddFile("Code/dbo/GetUser.breaking.sql", """
				if object_id('dbo.GetUser_v1') is null
					alter table dbo.Person drop column if exists Phone;
				drop proc if exists dbo.GetUser_v1;
				""");

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => DeployWithFixtureAsync(project)
		);

		Assert.Equal(SchemaCheck.BreakingSecondDeployment, ex.Check);
		Assert.EndsWith("  missing column dbo.Person.Phone: varchar(20) collate SQL_Latin1_General_CP1_CI_AS null", ex.Message);
	}

	[Fact]
	public async Task Idempotent_code_breaking_script_passes()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", "create or alter proc dbo.GetUser_v1 as select 1 as user_id;")
			.AddFile("Code/dbo/GetUser.sql", "create or alter proc dbo.GetUser_v2 @UserId int as select @UserId as user_id;")
			.AddFile("Code/dbo/GetUser.breaking.sql", "drop proc if exists dbo.GetUser_v1;");

		await DeployWithFixtureAsync(project);
	}

	private async Task DeployAsync(TempProject project, UndoTestMode undoTestMode)
	{
		DbLiveBuilder builder = CreateBuilder(project);

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
		DeploymentTestFixture fixture = new(new FixtureBuilder(CreateBuilder(project)), dropDatabaseOnComplete: true);
		await fixture.InitializeAsync();

		try
		{
			await fixture.DeployAsync(_output);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private sealed class FixtureBuilder(DbLiveBuilder builder) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() => Task.FromResult(builder);

		public Assembly GetProjectAssembly() => typeof(FixtureBuilder).Assembly;
	}
}
