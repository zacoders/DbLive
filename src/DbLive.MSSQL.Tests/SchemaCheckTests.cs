using DbLive.Testing;
using DbLive.xunit;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The schema check of the deployment tests: a second breaking deployment does not change the schema.
/// A migration breaking script runs once.
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
	// without 'if exists': the deployment test does not run a migration breaking script a second time.
	private const string Breaking = "alter table dbo.Person drop column Phone;";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	[Fact]
	public async Task Breaking_script_runs_once()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", CreatePerson)
			.AddFile("Migrations/002.migration.sql", Migration)
			.AddFile("Migrations/002.breaking.sql", Breaking);

		await DeployWithFixtureAsync(project);
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
