using NSubstitute.Extensions;

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

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

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

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

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

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
			CreateMigration(2, "-- migration 2")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		await mockSet.DbLiveDA.Received(2).ExecuteNonQueryAsync(Arg.Any<string>());
		await mockSet.DbLiveDA.Received(1).SetSelfMigrationVersionAsync(1, Arg.Any<DateTime>());
		await mockSet.DbLiveDA.Received(1).SetSelfMigrationVersionAsync(2, Arg.Any<DateTime>());
	}

	[Fact]
	public async Task Deploy_applies_only_migrations_greater_than_current_version()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(1);

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
		CreateMigration(2, "-- migration 2"),
		CreateMigration(3, "-- migration 3")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		await mockSet.DbLiveDA.Received(2).ExecuteNonQueryAsync(Arg.Any<string>());

		await mockSet.DbLiveDA.DidNotReceive()
			.SetSelfMigrationVersionAsync(1, Arg.Any<DateTime>());

		await mockSet.DbLiveDA.Received(1)
			.SetSelfMigrationVersionAsync(2, Arg.Any<DateTime>());

		await mockSet.DbLiveDA.Received(1)
			.SetSelfMigrationVersionAsync(3, Arg.Any<DateTime>());
	}


	[Fact]
	public async Task Deploy_wraps_migrations_in_transaction()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([CreateMigration(1, "-- migration 1")]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		await mockSet.TransactionRunner.Received(1).ExecuteWithinTransactionAsync(
			true,
			TranIsolationLevel.Serializable,
			TimeSpan.FromMinutes(5),
			Arg.Any<Func<Task>>()
		);
	}

	[Fact]
	public async Task Deploy_executes_sql_from_internal_migration_file_data()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- exact sql")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		await deployer.DeployAsync();

		// Assert
		await mockSet.DbLiveDA.Received(1).ExecuteNonQueryAsync("-- exact sql");
	}


	[Fact]
	public async Task Deploy_reads_the_database_inside_its_transaction()
	{
		MockSet mockSet = new();
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([CreateMigration(1, "-- migration 1")]);
		// the transaction does not run its body.
		mockSet.TransactionRunner
			.Configure()
			.ExecuteWithinTransactionAsync(default, default, default, default!)
			.ReturnsForAnyArgs(Task.CompletedTask);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		await deployer.DeployAsync();

		_ = await mockSet.DbLiveDA.DidNotReceive().DbLiveInstalledAsync();
		_ = await mockSet.DbLiveDA.DidNotReceive().GetMinDbLiveLibVersionAsync();
		_ = await mockSet.DbLiveDA.DidNotReceive().GetSelfMigrationVersionAsync();
	}

	[Fact]
	public async Task Deploy_fails_before_any_change_when_the_database_requires_a_newer_DbLive()
	{
		Version newerVersion = NewerThanMinCompatibleVersion();

		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(1);
		mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync().Returns(newerVersion);
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
			CreateMigration(2, "-- migration 2")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		DatabaseRequiresNewerDbLiveException ex = await Assert.ThrowsAsync<DatabaseRequiresNewerDbLiveException>(deployer.DeployAsync);

		Assert.Equal(newerVersion, ex.RequiredVersion);
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, ex.MinCompatibleVersion);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(default!, default, default);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetSelfMigrationVersionAsync(default, default);
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetMinDbLiveLibVersionAsync(default!);
	}

	[Fact]
	public async Task Deploy_stores_MinCompatibleVersion_after_the_self_migrations_of_a_new_database()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
			CreateMigration(2, "-- migration 2")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		await deployer.DeployAsync();

		Received.InOrder(() =>
		{
			_ = mockSet.DbLiveDA.ExecuteNonQueryAsync("-- migration 1");
			_ = mockSet.DbLiveDA.SetSelfMigrationVersionAsync(1, new DateTime(2025, 1, 1));
			_ = mockSet.DbLiveDA.ExecuteNonQueryAsync("-- migration 2");
			_ = mockSet.DbLiveDA.SetSelfMigrationVersionAsync(2, new DateTime(2025, 1, 1));
			_ = mockSet.DbLiveDA.SetMinDbLiveLibVersionAsync(DbLiveCompatibility.MinCompatibleVersion);
		});
		// a database without the DbLive tables has no column to read.
		_ = await mockSet.DbLiveDA.DidNotReceive().GetMinDbLiveLibVersionAsync();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("0.0.1")]
	public async Task Deploy_raises_a_missing_or_lower_requirement_to_MinCompatibleVersion(string? storedVersion)
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(1);
		mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync().Returns(storedVersion is null ? null : Version.Parse(storedVersion));
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([CreateMigration(1, "-- migration 1")]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		await deployer.DeployAsync();

		await mockSet.DbLiveDA.Received(1).SetMinDbLiveLibVersionAsync(DbLiveCompatibility.MinCompatibleVersion);
	}

	[Fact]
	public async Task Deploy_does_not_write_the_requirement_when_the_database_has_it()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(1);
		mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync().Returns(DbLiveCompatibility.MinCompatibleVersion);
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns([CreateMigration(1, "-- migration 1")]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		await deployer.DeployAsync();

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetMinDbLiveLibVersionAsync(default!);
	}

	[Fact]
	public async Task Deploy_goes_on_when_a_newer_DbLive_applied_more_self_migrations()
	{
		// a newer DbLive deployed the database, and its requirement allows this DbLive.
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(5);
		mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync().Returns(DbLiveCompatibility.MinCompatibleVersion);
		mockSet.IInternalDbLiveProject.GetMigrationsAsync().Returns(
		[
			CreateMigration(1, "-- migration 1"),
			CreateMigration(2, "-- migration 2"),
			CreateMigration(3, "-- migration 3")
		]);

		DbLiveSelfDeployer deployer = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		await deployer.DeployAsync();

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().ExecuteNonQueryAsync(default!, default, default);
		// the self-migration version is not lowered.
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetSelfMigrationVersionAsync(default, default);
		mockSet.Logger.Received(1).Information(
			Arg.Is<string>(m => m.StartsWith("The dblive schema of the database is at self-migration {AppliedVersion}, newer than the last one of this DbLive")),
			5L,
			3L,
			DbLiveCompatibility.MinCompatibleVersion
		);
	}

	private static Version NewerThanMinCompatibleVersion()
	{
		Version min = DbLiveCompatibility.MinCompatibleVersion;
		return new Version(min.Major, min.Minor + 1, 0);
	}

	private static InternalMigration CreateMigration(int version, string sql)
	{
		return new InternalMigration
		{
			Version = version,
			FileData = new FileData
			{
				Content = sql,
				RelativePath = $"project/{version}.migration.sql"
			}
		};
	}

}
