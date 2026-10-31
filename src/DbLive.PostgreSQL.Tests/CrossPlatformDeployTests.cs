using DbLive.Adapter;
using DbLive.Project;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CrossPlatformDeployTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	private const string MigrationCrLf = "create table t1 (\r\n    id int not null\r\n);\r\n";
	private const string CodeCrLf = "create or replace view v1\r\nas\r\nselect 1 as x;\r\n";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	[Fact]
	public async Task Database_deployed_by_older_DbLive_from_Windows_deploys_from_Linux_checkout()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", MigrationCrLf)
			.AddFile("Code/public/v1.sql", CodeCrLf);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// what DbLive stored on Windows before line endings and separators were normalized.
		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync($"""
			update dblive.migration
			set content_hash = {LegacyContentHash.Of(MigrationCrLf)}, relative_path = 'Migrations\001.migration.sql'
			where version = 1 and item_type = 'migration';

			update dblive.code
			set content_hash = {LegacyContentHash.Of(CodeCrLf)}, relative_path = 'Code\public\v1.sql';

			update dblive.version set version = 4;
			""");

		CodeItemDto codeBefore = (await da.FindCodeItemAsync(@"Code\public\v1.sql"))!;

		// the Linux checkout of the same files.
		_ = project
			.AddFile("Migrations/001.migration.sql", MigrationCrLf.Replace("\r\n", "\n"))
			.AddFile("Code/public/v1.sql", CodeCrLf.Replace("\r\n", "\n"));

		// MigrationChecksumMode is Strict by default.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal("Migrations/001.migration.sql", items.Single(i => i.Version == 1 && i.ItemType == MigrationItemType.Migration).RelativePath);

		// found by the new path and not redeployed.
		CodeItemDto? code = await da.FindCodeItemAsync("Code/public/v1.sql");
		Assert.NotNull(code);
		Assert.Equal(codeBefore.AppliedUtc, code.AppliedUtc);
		_ = Assert.NotNull(code.VerifiedUtc);
		Assert.Equal(1, await ScalarAsync(da, "select count(*) from dblive.code"));
	}

	[Fact]
	public async Task Self_deploy_keeps_the_latest_row_of_a_file_deployed_from_Windows_and_Linux()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync("""
			insert into dblive.code (relative_path, status, execution_time_ms, applied_utc, created_utc, verified_utc, content_hash)
			values ('Code\public\v1.sql', 'applied', 1, '2026-01-01', '2026-01-01', '2026-01-03', 1) -- verified last
			     , ('Code/public/v1.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 2)
			     , ('Code\public\v2.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 3)
			     , ('Code/public/v2.sql', 'applied', 1, '2026-01-02', '2026-01-02', null, 4) -- applied last
			     , ('Code\public\v3.sql', 'applied', 1, '2026-01-01', '2026-01-01', null, 5);

			insert into dblive.unit_test_run (relative_path, content_hash, run_utc, execution_time_ms, pass)
			values ('Tests\t1.sql', 1, '2026-01-02', 1, true)
			     , ('Tests/t1.sql', 2, '2026-01-01', 1, false);

			insert into dblive.folder_items (folder_type, relative_path, content_hash, created_utc, started_utc, completed_utc, execution_time_ms)
			values ('AfterDeploy', 'AfterDeploy\a.sql', 1, '2026-01-01', '2026-01-01', '2026-01-01', 1)
			     , ('AfterDeploy', 'AfterDeploy/a.sql', 2, '2026-01-02', '2026-01-02', '2026-01-02', 1)
			     , ('BeforeDeploy', 'AfterDeploy\a.sql', 3, '2026-01-01', '2026-01-01', '2026-01-01', 1);

			update dblive.version set version = 4;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			["Code/public/v1.sql:1", "Code/public/v2.sql:4", "Code/public/v3.sql:5"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.code order by relative_path")
		);
		Assert.Equal(
			["Tests/t1.sql:1"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.unit_test_run")
		);
		Assert.Equal(
			["AfterDeploy/a.sql:2", "AfterDeploy/a.sql:3"],
			await RowsAsync(da, "select relative_path, content_hash from dblive.folder_items order by content_hash")
		);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

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
