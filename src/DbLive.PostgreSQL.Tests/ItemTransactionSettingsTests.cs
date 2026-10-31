using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Project;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ItemTransactionSettingsTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	private const string NoTransaction = """{ "TransactionWrapLevel": "None" }""";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	[Fact]
	public async Task Each_item_runs_by_its_own_settings_in_undo_test_breaking_and_downgrade()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null, name text null);")
			.AddFile("Migrations/001.undo.sql", "drop table if exists t1;")
			// 'create/drop index concurrently' and 'vacuum' cannot run inside a transaction block.
			.AddFile("Migrations/002.migration.sql", "create index concurrently if not exists ix_t1_name on t1 (name);")
			.AddFile("Migrations/002.migration.settings.json", NoTransaction)
			.AddFile("Migrations/002.undo.sql", "drop index concurrently if exists ix_t1_name;")
			.AddFile("Migrations/002.u.s.json", NoTransaction)
			.AddFile("Migrations/003.migration.sql", "alter table t1 add column if not exists code text null;")
			.AddFile("Migrations/003.breaking.sql", "vacuum t1;")
			.AddFile("Migrations/003.b.s.json", NoTransaction)
			.AddFile("Migrations/003.undo.sql", "alter table t1 drop column if exists code;");

		DbLiveBuilder builder = CreateBuilder(project);

		// migration, breaking, undo and migration again, each by its own settings.
		await builder.CreateDeployer().DeployAsync(new DeployParameters
		{
			DeployBreaking = true,
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		});

		IDbLiveDA da = builder.CreateDbLiveDA();

		Assert.Equal(1, await CountAsync(da, "select count(*) from pg_indexes where indexname = 'ix_t1_name'"));
		Assert.Equal(1, await CountAsync(da, "select count(*) from information_schema.columns where table_name = 't1' and column_name = 'code'"));

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 3, MigrationItemType.Breaking).Status);
		Assert.Equal(MigrationItemStatus.None, GetItem(items, 2, MigrationItemType.UndoSettings).Status);
		Assert.Contains(items, i => i.Version == 2 && i.ItemType == MigrationItemType.MigrationSettings);
		Assert.Contains(items, i => i.Version == 3 && i.ItemType == MigrationItemType.BreakingSettings);
		Assert.Equal(NoTransaction, await da.GetMigrationContentAsync(2, MigrationItemType.UndoSettings));

		// the older build: 002 and 003 are undone by the scripts and settings stored in the database.
		_ = project.DeleteFiles("002.*").DeleteFiles("003.*");

		await builder.CreateDeployer().DeployAsync(new DeployParameters
		{
			AllowDatabaseDowngrade = true
		});

		Assert.Equal(0, await CountAsync(da, "select count(*) from pg_indexes where indexname = 'ix_t1_name'"));
		Assert.Equal(0, await CountAsync(da, "select count(*) from information_schema.columns where table_name = 't1' and column_name = 'code'"));

		items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Reverted, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Undo).Status);
		Assert.Equal(MigrationItemStatus.Reverted, GetItem(items, 3, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.Reverted, GetItem(items, 3, MigrationItemType.Breaking).Status);
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 3, MigrationItemType.Undo).Status);
	}

	[Fact]
	public async Task Item_without_settings_runs_in_a_transaction()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);")
			.AddFile("Migrations/002.migration.sql", "vacuum t1;");

		DbLiveBuilder builder = CreateBuilder(project);

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Contains("002.migration.sql", ex.Message);
		Assert.Contains("transaction block", ex.Message);
	}

	[Fact]
	public async Task Self_deploy_renames_old_settings_rows()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);")
			.AddFile("Migrations/001.migration.settings.json", NoTransaction);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// a database deployed by DbLive before the per-item settings: one 'settings' row per version.
		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync("""
			update dblive.migration set item_type = 'settings' where item_type = 'migration.settings';
			update dblive.version set version = 3;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(0, await CountAsync(da, "select count(*) from dblive.migration where item_type = 'settings'"));

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(
			[MigrationItemType.Migration, MigrationItemType.MigrationSettings],
			items.Where(i => i.Version == 1).Select(i => i.ItemType).Order()
		);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.ConfigureServices(s => s.AddSingleton(project.ProjectPath));
	}

	private static MigrationItemDto GetItem(IReadOnlyCollection<MigrationItemDto> items, long version, MigrationItemType itemType)
		=> items.Single(i => i.Version == version && i.ItemType == itemType);

	private static async Task<long> CountAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
