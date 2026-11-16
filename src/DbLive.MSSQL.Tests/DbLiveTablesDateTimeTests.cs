using DbLive.Adapter;
using DbLive.Project;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// The dates of the dblive tables are <c>datetime2(7)</c> and keep the 100 ns of a <see cref="DateTime"/>. Dapper sends a
/// <see cref="DateTime"/> as <c>datetime</c>, rounded to 1/300 of a second, unless the parameter says <c>datetime2</c>.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DbLiveTablesDateTimeTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public async Task Dates_written_to_the_dblive_tables_keep_their_100_ns()
	{
		// datetime would store .1233333.
		DateTime utc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1_234_567);

		// a project without scripts: the deployment creates the dblive tables and writes no row of the project.
		using TempProject project = new TempProject().AddFile("settings.json", "{}");

		DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName())
			.SetScriptsFolder(project.Path);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		await da.SetSelfMigrationVersionAsync(await da.GetSelfMigrationVersionAsync(), utc);

		await da.SaveMigrationItemAsync(new MigrationItemSaveDto
		{
			Version = 1,
			Name = "m",
			RelativePath = "Migrations/001.m.migration.sql",
			ItemType = MigrationItemType.Migration,
			Status = MigrationItemStatus.None,
			ContentHash = 1,
			CreatedUtc = utc
		});
		await da.UpdateMigrationStateAsync(new MigrationItemStateDto
		{
			Version = 1,
			ItemType = MigrationItemType.Migration,
			Status = MigrationItemStatus.Applied,
			AppliedUtc = utc,
			ExecutionTimeMs = 1
		});

		foreach (string path in new[] { "Code/a.sql", "Code/b.sql" })
		{
			await da.SaveCodeItemAsync(new CodeItemDto
			{
				RelativePath = path,
				ItemType = CodeItemType.Code,
				ContentHash = 1,
				Status = CodeItemStatus.Applied,
				AppliedUtc = utc,
				CreatedUtc = utc
			});
		}
		await da.MarkCodeAsVerifiedAsync(["Code/a.sql"], utc);
		_ = await da.MarkMissingCodeAsRemovedAsync(["Code/a.sql"], utc);

		await da.SaveUnitTestResultAsync(new UnitTestItemDto
		{
			RelativePath = "Tests/t.sql",
			ContentHash = 1,
			StartedUtc = utc,
			ExecutionTimeMs = 1,
			IsSuccess = true
		});

		await da.MarkItemAsAppliedAsync(ProjectFolder.AfterDeploy, "AfterDeploy/a.sql", utc, utc, 1, 1);

		List<SqlResult> results = await da.QuerySqlAsync("""
			select 'version.applied_utc', applied_utc from dblive.version
			union all select 'migration.created_utc', created_utc from dblive.migration
			union all select 'migration.applied_utc', applied_utc from dblive.migration
			union all select concat('code.applied_utc ', relative_path), applied_utc from dblive.code
			union all select concat('code.created_utc ', relative_path), created_utc from dblive.code
			union all select concat('code.verified_utc ', relative_path), verified_utc from dblive.code
			union all select 'unit_test_run.run_utc', run_utc from dblive.unit_test_run
			union all select 'folder_items.started_utc', started_utc from dblive.folder_items
			union all select 'folder_items.completed_utc', completed_utc from dblive.folder_items
			order by 1
			""");

		const string Stamp = "2026-01-02 03:04:05.1234567";
		Assert.Equal(
			[
				$"code.applied_utc code/a.sql {Stamp}",
				$"code.applied_utc code/b.sql {Stamp}",
				$"code.created_utc code/a.sql {Stamp}",
				$"code.created_utc code/b.sql {Stamp}",
				$"code.verified_utc code/a.sql {Stamp}",
				$"code.verified_utc code/b.sql {Stamp}",
				$"folder_items.completed_utc {Stamp}",
				$"folder_items.started_utc {Stamp}",
				$"migration.applied_utc {Stamp}",
				$"migration.created_utc {Stamp}",
				$"unit_test_run.run_utc {Stamp}",
				$"version.applied_utc {Stamp}"
			],
			results.Single().Rows.Select(r => $"{r[0]} {(DateTime)r[1]:yyyy-MM-dd HH:mm:ss.fffffff}")
		);
	}
}
