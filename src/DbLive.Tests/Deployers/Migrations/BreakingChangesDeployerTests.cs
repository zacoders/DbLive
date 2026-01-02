
namespace DbLive.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
	[Fact]
	public void Does_nothing_when_deploy_breaking_is_disabled()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deployer.Deploy(new DeployParameters { DeployBreaking = false });

		// Assert
		mockSet.DbLiveDA.DidNotReceive().GetMigrations();
		mockSet.DbLiveProject.DidNotReceive().GetMigrations();
		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public void Logs_and_returns_when_no_breaking_items_exist()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrations().Returns([]);

		mockSet.DbLiveProject.GetMigrations().Returns([
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

		var deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deployer.Deploy(DeployParameters.Breaking);

		// Assert
		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public void Applies_only_breaking_migrations_newer_than_latest_applied()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrations().Returns([
			new MigrationItemDto
			{
				Version = 2,
				Name = "breaking-v2",
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.Applied,
				ContentHash = "-- content 2".Crc32HashCode(),
				Content = "-- content 2"
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

		mockSet.DbLiveProject.GetMigrations().Returns([migration2, migration3]);

		var deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deployer.Deploy(DeployParameters.Breaking);

		// Assert
		mockSet.MigrationItemDeployer.Received(1)
			.Deploy(3, migration3.Items[MigrationItemType.Breaking]);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(2, Arg.Any<MigrationItem>());
	}

	[Fact]
	public void Applies_all_new_breaking_items_in_version_order()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrations().Returns([]);

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

		mockSet.DbLiveProject.GetMigrations().Returns([migration1, migration2]);

		var deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deployer.Deploy(DeployParameters.Breaking);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer
				.Deploy(1, migration1.Items[MigrationItemType.Breaking]);

			mockSet.MigrationItemDeployer
				.Deploy(2, migration2.Items[MigrationItemType.Breaking]);
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