using DbLive.Adapter;
using DbLive.Deployers.Migrations;
using DbLive.Project;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ItemTransactionSettingsTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string NoTransaction = """{ "TransactionWrapLevel": "None" }""";

	private const string SnapshotIsolationStateQuery = "select snapshot_isolation_state from sys.databases where name = db_name()";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveInternalDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Each_item_runs_by_its_own_settings_in_the_migration_and_breaking_phases()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			// 'alter database' is not allowed within a multi-statement transaction.
			.AddFile("Migrations/002.migration.sql", "alter database current set allow_snapshot_isolation on;")
			.AddFile("Migrations/002.migration.settings.json", NoTransaction)
			.AddFile("Migrations/003.migration.sql", "alter table dbo.t1 add code nvarchar(10) null;")
			.AddFile("Migrations/003.breaking.sql", "alter database current set recovery simple;")
			.AddFile("Migrations/003.b.s.json", NoTransaction);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(new DeployParameters { DeployBreaking = true });

		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();

		Assert.Equal(1, await ScalarAsync(da, SnapshotIsolationStateQuery));
		Assert.Equal(1, await ScalarAsync(da, "select count(*) from sys.columns where object_id = object_id('dbo.t1') and name = 'code'"));

		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 3, MigrationItemType.Breaking).Status);
	}

	[Fact]
	public async Task Older_build_fails_before_it_changes_anything()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Migrations/002.migration.sql", "alter table dbo.t1 add code nvarchar(10) null;")
			.AddFile("Migrations/003.migration.sql", "create table dbo.t3 ( id int not null );")
			.AddFile("Migrations/003.breaking.sql", "alter table dbo.t1 drop column code;");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
		string migrationRowsQuery = "select count(*) from dblive.migration";
		long migrationRows = await ScalarAsync(da, migrationRowsQuery);

		// the build of the release before: no 003, and a new BeforeDeploy script.
		_ = project.DeleteFiles("003.*").AddFile("BeforeDeploy/drop-t1.sql", "drop table dbo.t1;");
		builder = CreateBuilder(project);

		foreach (DeployParameters parameters in new[] { DeployParameters.Default, DeployParameters.Breaking })
		{
			DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(
				() => builder.CreateDeployer().DeployAsync(parameters)
			);
			Assert.Equal([3], ex.Versions);
		}

		Assert.Equal(1, await ScalarAsync(da, "select count(*) from sys.columns where object_id = object_id('dbo.t1') and name = 'code'"));
		Assert.Equal(migrationRows, await ScalarAsync(da, migrationRowsQuery));
	}

	[Fact]
	public async Task Item_without_settings_runs_in_a_transaction()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "alter database current set allow_snapshot_isolation on;");

		DbLiveBuilder builder = CreateBuilder(project);

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Contains("001.migration.sql", ex.Message);
		Assert.Contains("transaction", ex.Message);
	}

	[Fact]
	public async Task Self_deploy_renames_old_settings_rows()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Migrations/001.migration.settings.json", NoTransaction);

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// a database deployed by DbLive before the per-item settings: one 'settings' row per version.
		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
		await da.ExecuteNonQueryAsync("""
			update dblive.migration set item_type = 'settings' where item_type = 'migration.settings';
			update dblive.version set version = 6;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(0, await ScalarAsync(da, "select count(*) from dblive.migration where item_type = 'settings'"));

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
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Migrations/002.migration.sql", "create table dbo.t2 ( id int not null );")
			.AddFile("Migrations/002.breaking.sql", "-- nothing to drop");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// a database deployed by a DbLive with undo scripts: undo rows with their content, and version 2 rolled back by its undo script.
		IDbLiveInternalDA da = builder.GetRequiredService<IDbLiveInternalDA>();
		await da.ExecuteNonQueryAsync("""
			alter table dblive.migration add content nvarchar(max) null;
			go
			insert into dblive.migration ( version, item_type, name, relative_path, status, content_hash, content, created_utc, applied_utc )
			values ( 1, 'undo', '', 'migrations/001.undo.sql', 'none', 1, 'drop table dbo.t1;', sysutcdatetime(), null )
			     , ( 2, 'undo', '', 'migrations/002.undo.sql', 'applied', 2, 'drop table dbo.t2;', sysutcdatetime(), sysutcdatetime() )
			     , ( 2, 'undo.settings', '', 'migrations/002.undo.settings.json', 'none', 3, '{}', sysutcdatetime(), null );
			update dblive.migration set status = 'reverted', applied_utc = null where version = 2 and item_type in ( 'migration', 'breaking' );
			drop table dbo.t2;
			update dblive.version set version = 10;
			""");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(0, await ScalarAsync(da, "select count(*) from dblive.migration where item_type like 'undo%' or status = 'reverted'"));
		Assert.Equal(0, await ScalarAsync(da, "select count(*) from sys.columns where object_id = object_id('dblive.migration') and name = 'content'"));

		// the rolled back version is deployed again: its rows are saved again and its migration runs.
		Assert.Equal(1, await ScalarAsync(da, "select count(*) from sys.tables where object_id = object_id('dbo.t2')"));
		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		Assert.Equal(MigrationItemStatus.Applied, GetItem(items, 2, MigrationItemType.Migration).Status);
		Assert.Equal(MigrationItemStatus.None, GetItem(items, 2, MigrationItemType.Breaking).Status);
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

	private static MigrationItemDto GetItem(IReadOnlyCollection<MigrationItemDto> items, long version, MigrationItemType itemType)
		=> items.Single(i => i.Version == version && i.ItemType == itemType);

	private static async Task<long> ScalarAsync(IDbLiveInternalDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return Convert.ToInt64(results.Single().Rows.Single()[0]);
	}
}
