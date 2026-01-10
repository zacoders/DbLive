namespace DbLive.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
	[Fact]
	public async Task Does_nothing_when_deploy_breaking_is_disabled()
	{
		// Arrange
		MockSet mockSet = new();

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(new DeployParameters { DeployBreaking = false });

		// Assert
		await mockSet.DbLiveDA.DidNotReceive().GetMigrationsAsync();
		await mockSet.DbLiveProject.DidNotReceive().GetMigrationsAsync();
		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Logs_and_returns_when_no_breaking_items_exist()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			new Migration
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("001.m.sql", "-- content")
				}
			}
		}
		]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Applies_only_breaking_migrations_newer_than_latest_applied()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			new MigrationItemDto
			{
				Version = 2,
				Name = "breaking-v2",
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.Applied,
				ContentHash = "-- content 2".ComputeFileHash(),
				RelativePath = "migrations/2.breaking.sql"
			}
		]);

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				}
			}
		};

		Migration migration3 = new()
		{
			Version = 3,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("3.breaking.sql", "-- content 3")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration2, migration3]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(3, migration3.Items[MigrationItemType.Breaking]);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(2, Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Applies_all_new_breaking_items_in_version_order()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		Migration migration1 = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("1.breaking.sql", "-- content 1")
				}
			}
		};

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration1, migration2]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer
				.DeployAsync(1, migration1.Items[MigrationItemType.Breaking]);

			mockSet.MigrationItemDeployer
				.DeployAsync(2, migration2.Items[MigrationItemType.Breaking]);
		});
	}

	private static FileData GetFileData(string relativePath, string content)
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath,
			FilePath = "c:/db/migrations" + relativePath
		};
	}
}