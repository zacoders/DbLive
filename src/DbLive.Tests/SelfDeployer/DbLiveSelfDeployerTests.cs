using System.Threading.Tasks;

namespace DbLive.Tests.SelfDeployer;


public class DbLiveSelfDeployerTests
{
	[Fact]
	public async Task Deploy_logs_start_and_finish_when_logging_enabled()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			LogSelfDeploy = true
		});

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(false);

		var deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		mockSet.Logger.Received(1).Information("Starting self deploy.");
		mockSet.Logger.Received(1).Information("Self deploy completed.");
	}

	[Fact]
	public async Task Deploy_does_not_log_when_logging_disabled()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			LogSelfDeploy = false
		});

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(false);

		var deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		mockSet.Logger.DidNotReceive().Information(Arg.Any<string>());
	}

	[Fact]
	public async Task Deploy_executes_all_migrations_when_dblive_not_installed()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		mockSet.DbLiveDA.DbLiveInstalled().Returns(false);

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
			CreateMigration(2, "-- migration 2")
		]);

		var deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		mockSet.DbLiveDA.Received(2).ExecuteNonQuery(Arg.Any<string>());
		mockSet.DbLiveDA.Received(1).SetDbLiveVersion(1, Arg.Any<DateTime>());
		mockSet.DbLiveDA.Received(1).SetDbLiveVersion(2, Arg.Any<DateTime>());
	}

	[Fact]
	public async Task Deploy_applies_only_migrations_greater_than_current_version()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
		CreateMigration(2, "-- migration 2"),
		CreateMigration(3, "-- migration 3")
		]);

		var deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		mockSet.DbLiveDA.Received(2).ExecuteNonQuery(Arg.Any<string>());

		mockSet.DbLiveDA.DidNotReceive()
			.SetDbLiveVersion(1, Arg.Any<DateTime>());

		mockSet.DbLiveDA.Received(1)
			.SetDbLiveVersion(2, Arg.Any<DateTime>());

		mockSet.DbLiveDA.Received(1)
			.SetDbLiveVersion(3, Arg.Any<DateTime>());
	}


	[Fact]
	public async Task Deploy_executes_sql_from_internal_migration_file_data()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		mockSet.DbLiveDA.DbLiveInstalled().Returns(false);

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- exact sql")
		]);

		var deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		mockSet.DbLiveDA.Received(1).ExecuteNonQuery("-- exact sql");
	}


	private static InternalMigration CreateMigration(int version, string sql)
	{
		return new InternalMigration
		{
			Version = version,
			FileData = new FileData
			{
				Content = sql,
				FilePath = $"c:/project/{version}.migration.sql",
				RelativePath = $"project/{version}.migration.sql"
			}
		};
	}

}
