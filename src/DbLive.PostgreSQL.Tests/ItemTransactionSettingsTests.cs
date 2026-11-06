using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Project;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ItemTransactionSettingsTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private const string NoTransaction = """{ "TransactionWrapLevel": "None" }""";

	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).CreateDbLiveDA().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Each_item_runs_by_its_own_settings_in_the_migration_and_breaking_phases()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null, name text null);")
			// 'create index concurrently' and 'vacuum' cannot run inside a transaction block.
			.AddFile("Migrations/002.migration.sql", "create index concurrently if not exists ix_t1_name on t1 (name);")
			.AddFile("Migrations/002.migration.settings.json", NoTransaction)
			.AddFile("Migrations/003.migration.sql", "alter table t1 add column if not exists code text null;")
			.AddFile("Migrations/003.breaking.sql", "vacuum t1;")
			.AddFile("Migrations/003.b.s.json", NoTransaction);

		DbLiveBuilder builder = CreateBuilder(project);

		// the breaking script runs twice, as in the deployment tests: both runs by its settings.
		await builder.CreateDeployer().DeployAsync(new DeployParameters
		{
			DeployBreaking = true,
			CheckBreakingSecondRun = true
		});

		IDbLiveDA da = builder.CreateDbLiveDA();

		Assert.Equal(1, await CountAsync(da, "select count(*) from pg_indexes where indexname = 'ix_t1_name'"));
		Assert.Equal(1, await CountAsync(da, "select count(*) from information_schema.columns where table_name = 't1' and column_name = 'code'"));

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 3, MigrationItemType.Breaking).Status);
		Assert.Contains(items, i => i.Version == 2 && i.ItemType == MigrationItemType.MigrationSettings);
		Assert.Contains(items, i => i.Version == 3 && i.ItemType == MigrationItemType.BreakingSettings);
	}

	[Fact]
	public async Task Older_build_fails_before_it_changes_anything()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);")
			.AddFile("Migrations/002.migration.sql", "alter table t1 add column code text null;")
			.AddFile("Migrations/003.migration.sql", "create table t3 (id int not null);")
			.AddFile("Migrations/003.breaking.sql", "alter table t1 drop column code;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.CreateDbLiveDA();
		string migrationRowsQuery = "select count(*) from dblive.migration";
		long migrationRows = await CountAsync(da, migrationRowsQuery);

		// the build of the release before: no 003, and a new BeforeDeploy script.
		_ = project.DeleteFiles("003.*").AddFile("BeforeDeploy/drop-t1.sql", "drop table t1;");

		foreach (DeployParameters parameters in new[] { DeployParameters.Default, DeployParameters.Breaking })
		{
			DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(
				() => builder.CreateDeployer().DeployAsync(parameters)
			);
			Assert.Equal([3], ex.Versions);
		}

		Assert.Equal(1, await CountAsync(da, "select count(*) from information_schema.columns where table_name = 't1' and column_name = 'code'"));
		Assert.Equal(migrationRows, await CountAsync(da, migrationRowsQuery));
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
	public async Task Scripts_without_a_transaction_run_at_the_configured_isolation_level()
	{
		const string Log = "insert into iso_log values ('{0}', current_setting('transaction_isolation'));";

		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionIsolationLevel": "RepeatableRead" }""")
			.AddFile("Migrations/001.migration.sql", "create table iso_log (item text not null, level text not null);")
			.AddFile("Migrations/002.migration.sql", string.Format(Log, "migration"))
			.AddFile("Migrations/002.migration.settings.json", """{ "TransactionWrapLevel": "None", "TransactionIsolationLevel": "Serializable" }""")
			.AddFile("Code/log.sql", string.Format(Log, "code"))
			.AddFile("AfterDeploy/log.sql", string.Format(Log, "after deploy"));

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		List<SqlResult> results = await builder.CreateDbLiveDA().ExecuteQueryMultipleAsync("select item || ': ' || level from iso_log order by item");
		Assert.Equal(
			["after deploy: repeatable read", "code: repeatable read", "migration: serializable"],
			results.Single().Rows.Select(r => (string)r[0])
		);
	}

	[Fact]
	public async Task Isolation_level_does_not_stay_on_the_pooled_connection()
	{
		// one connection in the pool: the next open gets the same session.
		string cnnString = new NpgsqlConnectionStringBuilder(_dbCnnString) { MaxPoolSize = 1 }.ConnectionString;
		IDbLiveDA da = new DbLiveBuilder().PostgreSQL().SetDbConnection(cnnString).CreateDbLiveDA();

		List<SqlResult> results = await da.ExecuteQueryMultipleAsync("select pg_backend_pid(), current_setting('transaction_isolation')", TranIsolationLevel.Serializable);
		int pid = (int)results.Single().Rows.Single()[0];
		Assert.Equal("serializable", results.Single().Rows.Single()[1]);

		await using NpgsqlConnection cnn = new(cnnString);
		await cnn.OpenAsync();
		await using NpgsqlCommand cmd = new("select pg_backend_pid(), current_setting('transaction_isolation')", cnn);
		await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
		Assert.True(await reader.ReadAsync());

		Assert.Equal(pid, reader.GetInt32(0));
		Assert.Equal("read committed", reader.GetString(1));
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

	[Fact]
	public async Task Self_deploy_removes_undo_rows_and_the_content_column()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);")
			.AddFile("Migrations/002.migration.sql", "create table t2 (id int not null);")
			.AddFile("Migrations/002.breaking.sql", "-- nothing to drop");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// a database deployed by a DbLive with undo scripts: undo rows with their content, and version 2 rolled back by its undo script.
		IDbLiveDA da = builder.CreateDbLiveDA();
		await da.ExecuteNonQueryAsync("""
			alter table dblive.migration add column content text null;
			insert into dblive.migration (version, item_type, name, relative_path, status, content_hash, content, created_utc, applied_utc)
			values (1, 'undo', '', 'migrations/001.undo.sql', 'none', 1, 'drop table t1;', now(), null),
			       (2, 'undo', '', 'migrations/002.undo.sql', 'applied', 2, 'drop table t2;', now(), now()),
			       (2, 'undo.settings', '', 'migrations/002.undo.settings.json', 'none', 3, '{}', now(), null);
			update dblive.migration set status = 'reverted', applied_utc = null where version = 2 and item_type in ('migration', 'breaking');
			drop table t2;
			update dblive.version set version = 7;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(0, await CountAsync(da, "select count(*) from dblive.migration where item_type like 'undo%' or status = 'reverted'"));
		Assert.Equal(0, await CountAsync(da, "select count(*) from information_schema.columns where table_schema = 'dblive' and table_name = 'migration' and column_name = 'content'"));

		// the rolled back version is deployed again: its rows are saved again and its migration runs.
		Assert.Equal(1, await CountAsync(da, "select count(*) from information_schema.tables where table_name = 't2'"));
		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.None, GetItem(items, 2, MigrationItemType.Breaking).Status);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static MigrationItemDto GetItem(IReadOnlyCollection<MigrationItemDto> items, long version, MigrationItemType itemType)
		=> items.Single(i => i.Version == version && i.ItemType == itemType);

	private static async Task<long> CountAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
