using DbLive.Adapter;
using DbLive.SelfDeployer;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// A newer DbLive can change the dblive tables in a way an older one cannot read. The database keeps the oldest DbLive version
/// that can deploy it (<c>dblive.version.min_dblive_lib_version</c>), and an older DbLive stops before it changes anything.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class NewerDbLiveDatabaseTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Database_that_requires_a_newer_DbLive_fails_the_deployment_and_the_tests_and_changes_nothing()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Tests/t1.test.sql", "select 'empty' as assert;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, await da.GetMinDbLiveLibVersionAsync());

		// what a newer DbLive stores after a change of the dblive tables that this DbLive cannot read.
		await da.ExecuteSqlAsync("update dblive.version set min_dblive_lib_version = '999.0.0';");
		string tablesBefore = await DescribeDbLiveTablesAsync(da);

		_ = project
			.AddFile("Migrations/002.migration.sql", "create table dbo.t2 ( id int not null );")
			.AddFile("Code/dbo/v1.sql", "create or alter view dbo.v1 as select 1 as x;");
		builder = CreateBuilder(project);

		DatabaseRequiresNewerDbLiveException ex = await Assert.ThrowsAsync<DatabaseRequiresNewerDbLiveException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default));

		Assert.Equal(new Version(999, 0, 0), ex.RequiredVersion);
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, ex.MinCompatibleVersion);

		_ = await Assert.ThrowsAsync<DatabaseRequiresNewerDbLiveException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Breaking));

		_ = await Assert.ThrowsAsync<DatabaseRequiresNewerDbLiveException>(builder.CreateTester().RunAllTestsAsync);

		Assert.Equal(tablesBefore, await DescribeDbLiveTablesAsync(da));
		Assert.Equal(0, await da.ScalarAsync("select count(*) from sys.objects where name in ( 't2', 'v1' )"));
	}

	[Fact]
	public async Task Database_with_self_migrations_of_a_newer_DbLive_deploys_when_its_requirement_allows()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		// a newer DbLive with a self-migration that this DbLive can live with: it keeps the requirement.
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		await da.ExecuteSqlAsync("update dblive.version set version = 1000;");

		_ = project.AddFile("Migrations/002.migration.sql", "create table dbo.t2 ( id int not null );");
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(1, await da.ScalarAsync("select count(*) from sys.tables where name = 't2'"));
		Assert.Equal(1000, await da.GetSelfMigrationVersionAsync());
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, await da.GetMinDbLiveLibVersionAsync());
	}

	[Fact]
	public async Task Database_deployed_before_the_requirement_gets_it()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		long lastSelfMigration = await da.GetSelfMigrationVersionAsync();

		// the dblive tables before self-migration 12.
		await da.ExecuteSqlAsync("""
			alter table dblive.version drop column min_dblive_lib_version;
			update dblive.version set version = 11;
			""");

		Assert.Null(await da.GetMinDbLiveLibVersionAsync());

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(lastSelfMigration, await da.GetSelfMigrationVersionAsync());
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, await da.GetMinDbLiveLibVersionAsync());
	}

	/// <summary>
	/// A builder reads the project once: a new build of the folder takes a new builder, like a new run of a release pipeline.
	/// </summary>
	private static async Task<string> DescribeDbLiveTablesAsync(IDbLiveDA da)
	{
		List<SqlResult> results = await da.QuerySqlAsync("""
			select * from dblive.version;
			select * from dblive.migration order by version, item_type;
			select * from dblive.code order by relative_path;
			select * from dblive.unit_test_run order by relative_path;
			select * from dblive.folder_items order by folder_type, relative_path;
			""");

		return string.Join(
			Environment.NewLine,
			results.Select((result, i) => $"{i}: " + string.Join("; ", result.Rows.Select(row => string.Join(" | ", row))))
		);
	}
}
