using DbLive.Adapter;
using DbLive.Testing;
using DbLive.Project;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The database has a case-sensitive collation, like PostgreSQL, so rows that differ only by case can exist in it.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CrossPlatformDeployTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string MigrationCrLf = "create table dbo.t1 ( id int not null );\r\ngo\r\n";
	private const string CodeCrLf = "create or alter view dbo.v1\r\nas\r\nselect 1 as x;\r\n";
	private const string Code = "create or alter view dbo.v1 as select 1 as x;";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	public async Task InitializeAsync()
	{
		// DbLive deploys to an existing database without creating it.
		using SqlConnection cnn = new(_fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = $"create database [{new SqlConnectionStringBuilder(_dbCnnString).InitialCatalog}] collate Latin1_General_100_CS_AS;";
		_ = await cmd.ExecuteNonQueryAsync();
	}

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Database_deployed_by_older_DbLive_from_Windows_deploys_from_Linux_checkout()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", MigrationCrLf)
			.AddFile("Code/dbo/v1.sql", CodeCrLf);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();

		// what DbLive stored on Windows before line endings, separators and case were normalized.
		await da.ExecuteNonQueryAsync($"""
			update dblive.migration
			set content_hash = {LegacyContentHash.Of(MigrationCrLf)}, relative_path = 'Migrations\001.migration.sql'
			where version = 1 and item_type = 'migration';

			update dblive.code
			set content_hash = {LegacyContentHash.Of(CodeCrLf)}, relative_path = 'Code\dbo\v1.sql';

			update dblive.version set version = 7;
			""");

		// the Linux checkout of the same files.
		_ = project
			.AddFile("Migrations/001.migration.sql", MigrationCrLf.Replace("\r\n", "\n"))
			.AddFile("Code/dbo/v1.sql", CodeCrLf.Replace("\r\n", "\n"));
		builder = CreateBuilder(project);

		// MigrationChecksumMode is Strict by default.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal("migrations/001.migration.sql", items.Single(i => i.Version == 1 && i.ItemType == MigrationItemType.Migration).RelativePath);

		// found by the new path. Only the current hash counts for code: the file deployed by an older DbLive from a CRLF checkout
		// runs once more, and its row gets the current hash.
		CodeItemDto? code = await FindCodeRowAsync(da, "code/dbo/v1.sql");
		Assert.NotNull(code);
		Assert.Equal(CodeItemStatus.Applied, code.Status);
		Assert.Equal(CodeCrLf.ComputeFileHash(), code.ContentHash);
		Assert.Equal(1, await ScalarAsync(da, "select count(*) from dblive.code"));
	}

	[Fact]
	public async Task Self_deploy_keeps_the_latest_row_of_a_file_deployed_from_Windows_and_Linux()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync("""
			insert into dblive.code ( relative_path, status, execution_time_ms, applied_utc, created_utc, verified_utc, content_hash )
			values ( 'Code\dbo\v1.sql', 'applied', 1, '2026-01-01', '2026-01-01', '2026-01-03', 1 ) -- verified last
			     , ( 'Code/dbo/v1.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 2 )
			     , ( 'Code\dbo\v2.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 3 )
			     , ( 'Code/dbo/v2.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 4 ) -- applied last
			     , ( 'Code\dbo\v3.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 5 );

			insert into dblive.unit_test_run ( relative_path, content_hash, run_utc, execution_time_ms, pass )
			values ( 'Tests\t1.sql', 1, '2026-01-02', 1, 1 )
			     , ( 'Tests/t1.sql', 2, '2026-01-01', 1, 0 );

			insert into dblive.folder_items ( folder_type, relative_path, content_hash, created_utc, started_utc, completed_utc, execution_time_ms )
			values ( 'AfterDeploy', 'AfterDeploy\a.sql', 1, '2026-01-01', '2026-01-01', '2026-01-01', 1 )
			     , ( 'AfterDeploy', 'AfterDeploy/a.sql', 2, '2026-01-02', '2026-01-02', '2026-01-02', 1 )
			     , ( 'BeforeDeploy', 'AfterDeploy\a.sql', 3, '2026-01-01', '2026-01-01', '2026-01-01', 1 );

			update dblive.version set version = 7;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			["code/dbo/v1.sql:1", "code/dbo/v2.sql:4", "code/dbo/v3.sql:5"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.code order by relative_path")
		);
		Assert.Equal(
			["tests/t1.sql:1"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.unit_test_run")
		);
		Assert.Equal(
			["afterdeploy/a.sql:2", "afterdeploy/a.sql:3"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.folder_items order by content_hash")
		);
	}

	[Fact]
	public async Task Self_deploy_lower_cases_relative_paths_and_keeps_the_latest_row_of_a_file_renamed_by_case()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// what DbLive stored before relative paths were lower-cased.
		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync("""
			insert into dblive.code ( relative_path, status, execution_time_ms, applied_utc, created_utc, verified_utc, content_hash )
			values ( 'Code/dbo/V1.sql', 'applied', 1, '2026-01-01', '2026-01-01', '2026-01-03', 1 ) -- verified last
			     , ( 'Code/dbo/v1.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 2 )
			     , ( 'Code/dbo/V2.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 3 )
			     , ( 'code/dbo/v2.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 4 ) -- applied last
			     , ( 'Code/dbo/V3.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 5 );

			insert into dblive.unit_test_run ( relative_path, content_hash, run_utc, execution_time_ms, pass )
			values ( 'Tests/T1.sql', 1, '2026-01-02', 1, 1 )
			     , ( 'Tests/t1.sql', 2, '2026-01-01', 1, 0 );

			insert into dblive.folder_items ( folder_type, relative_path, content_hash, created_utc, started_utc, completed_utc, execution_time_ms )
			values ( 'AfterDeploy', 'AfterDeploy/A.sql', 1, '2026-01-01', '2026-01-01', '2026-01-01', 1 )
			     , ( 'AfterDeploy', 'afterdeploy/a.sql', 2, '2026-01-02', '2026-01-02', '2026-01-02', 1 )
			     , ( 'BeforeDeploy', 'AfterDeploy/A.sql', 3, '2026-01-01', '2026-01-01', '2026-01-01', 1 );

			update dblive.migration set relative_path = 'Migrations/001.migration.sql';

			update dblive.version set version = 8;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			["code/dbo/v1.sql:1", "code/dbo/v2.sql:4", "code/dbo/v3.sql:5"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.code order by relative_path")
		);
		Assert.Equal(
			["tests/t1.sql:1"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.unit_test_run")
		);
		Assert.Equal(
			["afterdeploy/a.sql:2", "afterdeploy/a.sql:3"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.folder_items order by content_hash")
		);
		Assert.Equal(
			["migrations/001.migration.sql:1"],
			await RowsAsync(da, "select relative_path, version from dblive.migration")
		);
	}

	[Fact]
	public async Task New_database_stores_relative_paths_in_lower_case()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Code/dbo/V1.sql", Code)
			.AddFile("Tests/T1.sql", "select 1 as x;")
			.AddFile("BeforeDeploy/B.sql", "select 1 as x;")
			.AddFile("AfterDeploy/A.sql", "select 1 as x;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
		await builder.CreateTester().RunAllTestsAsync();

		IDbLiveDA da = builder.CreateDbLiveDA();
		Assert.Equal(["migrations/001.migration.sql:migration"], await RowsAsync(da, "select relative_path, item_type from dblive.migration"));
		Assert.Equal(["code/dbo/v1.sql:applied"], await RowsAsync(da, "select relative_path, status from dblive.code"));
		Assert.Equal(["tests/t1.sql:True"], await RowsAsync(da, "select relative_path, pass from dblive.unit_test_run"));
		Assert.Equal(
			["afterdeploy/a.sql:AfterDeploy", "beforedeploy/b.sql:BeforeDeploy"],
			await RowsAsync(da, "select relative_path, folder_type from dblive.folder_items order by relative_path")
		);
	}

	[Fact]
	public async Task Code_file_renamed_only_by_case_is_not_redeployed()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/dbo/V1.sql", Code);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
		CodeItemDto codeBefore = (await FindCodeRowAsync(da, "code/dbo/v1.sql"))!;

		_ = project
			.DeleteFiles("V1.sql")
			.AddFile("Code/dbo/v1.sql", Code);
		builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		CodeItemDto? code = await FindCodeRowAsync(da, "code/dbo/v1.sql");
		Assert.NotNull(code);
		Assert.Equal(codeBefore.AppliedUtc, code.AppliedUtc);
		_ = Assert.NotNull(code.VerifiedUtc);
		Assert.Equal(["code/dbo/v1.sql:applied"], await RowsAsync(da, "select relative_path, status from dblive.code"));
	}

	[Fact]
	public async Task Database_deployed_by_older_DbLive_with_the_folder_casing_of_the_settings_redeploys_nothing()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "CodeFolder": "code" }""")
			.AddFile("Code/dbo/V1.sql", Code);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
		CodeItemDto codeBefore = (await FindCodeRowAsync(da, "code/dbo/v1.sql"))!;

		// DbLive on Windows took the folder part of the path from the settings and the file name from the disk.
		await da.ExecuteNonQueryAsync("""
			update dblive.code set relative_path = 'code/dbo/V1.sql';

			update dblive.version set version = 8;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		CodeItemDto? code = await FindCodeRowAsync(da, "code/dbo/v1.sql");
		Assert.NotNull(code);
		Assert.Equal(codeBefore.AppliedUtc, code.AppliedUtc);
		Assert.Equal(["code/dbo/v1.sql:applied"], await RowsAsync(da, "select relative_path, status from dblive.code"));
	}

	[Fact]
	public async Task Failed_test_is_reported_by_the_real_path_and_saved_in_lower_case()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/Orders/Select-Order.sql", "throw 50000, 'Expected failure.', 1;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(
			builder.CreateTester().RunAllTestsAsync);

		Assert.Equal(["Tests/Orders/Select-Order.sql"], ex.FailedTests);
		Assert.Equal(
			["tests/orders/select-order.sql:False"],
			await RowsAsync(builder.CreateDbLiveDA(), "select relative_path, pass from dblive.unit_test_run")
		);
	}

	/// <summary>
	/// A builder reads the project once: a new build of the folder takes a new builder, like a new run of a release pipeline.
	/// </summary>
	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	// the row of a code file by its stored (lower-case) path.
	private static async Task<CodeItemDto?> FindCodeRowAsync(IDbLiveInternalDA da, string storedPath) =>
		(await da.GetCodeItemsAsync()).SingleOrDefault(row => row.RelativePath == storedPath);

	private static async Task<long> ScalarAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}

	private static async Task<List<string>> RowsAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return results.Single().Rows.Select(r => $"{r[0]}:{r[1]}").ToList();
	}
}
