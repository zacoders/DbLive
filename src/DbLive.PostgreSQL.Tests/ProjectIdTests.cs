using DbLive.Adapter;
using DbLive.Project.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The ProjectId binds a project to its database: a deployment to the database of another project fails before DbLive changes
/// anything in it, also before its self-migrations change the dblive tables.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ProjectIdTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	[Fact]
	public async Task Database_of_another_project_fails_before_DbLive_changes_its_tables()
	{
		using TempProject projectB = new TempProject()
			.AddFile("settings.json", """{ "ProjectId": "project-b" }""")
			.AddFile("Migrations/001.migration.sql", "create table b ( id int not null );");

		DbLiveBuilder builderB = CreateBuilder(projectB);
		await builderB.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		IDbLiveDA da = builderB.CreateDbLiveDA();
		Assert.Equal("project-b", await da.GetProjectIdAsync());

		// the dblive tables as an older DbLive left them: before self-migration 9, which the deployment of project A would apply.
		await da.ExecuteNonQueryAsync("""
			alter table dblive.version drop column min_dblive_lib_version;
			update dblive.version set version = 8;
			""");
		string tablesBefore = await DescribeDbLiveTablesAsync(da);

		using TempProject projectA = new TempProject()
			.AddFile("settings.json", """{ "ProjectId": "project-a" }""")
			.AddFile("Migrations/001.migration.sql", "create table a ( id int not null );");

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			() => CreateBuilder(projectA).CreateDeployer().DeployAsync(DeployParameters.Default));

		Assert.Equal(
			"ProjectId mismatch. Settings ProjectId: project-a, database ProjectId: project-b. Deployment blocked to prevent deploying to the wrong database.",
			ex.Message
		);
		Assert.Equal(tablesBefore, await DescribeDbLiveTablesAsync(da));
		Assert.Equal(0, await ScalarAsync(da, "select count(*) from pg_class where relname = 'a'"));
	}

	[Fact]
	public async Task Database_deployed_before_the_ProjectId_column_has_no_ProjectId()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "ProjectId": "project-a" }""")
			.AddFile("Migrations/001.migration.sql", "create table a ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		IDbLiveDA da = builder.CreateDbLiveDA();

		// the dblive tables before self-migration 3: the ProjectId is read before the self-deployment adds the column.
		await da.ExecuteNonQueryAsync("alter table dblive.version drop column project_id;");

		Assert.Null(await da.GetProjectIdAsync());
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<string> DescribeDbLiveTablesAsync(IDbLiveDA da)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync("""
			select * from dblive.version;
			select * from dblive.migration order by version, item_type;
			select * from dblive.code order by relative_path;
			select * from dblive.folder_items order by folder_type, relative_path;
			""");

		return string.Join(
			Environment.NewLine,
			results.Select((result, i) => $"{i}: " + string.Join("; ", result.Rows.Select(row => string.Join(" | ", row))))
		);
	}

	private static async Task<long> ScalarAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
