
using System.Threading.Tasks;

namespace DbLive.Tests.Deployers.Migrations;

public class MigrationVersionDeployerTests
{
	[Fact]
	public async Task DeployMigration_EmptyMigration()
	{
		var mockSet = new MockSet();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = []
		};

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => deploy.DeployAsync(migration, DeployParameters.Default)
		);
	}

	[Fact]
	public async Task DeployMigration()
	{
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		await deploy.DeployAsync(migration, DeployParameters.Default);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync( 1, Arg.Any<MigrationItem>());

	 	await mockSet.DbLiveDA.Received()
			.SetCurrentMigrationVersionAsync(migration.Version, new DateTime(2024, 1, 1));
	}

	[Fact]
	public async Task DeployMigration_SkipMigrationTypes()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.Undo] = new()
				{
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		// Act
		await deploy.DeployAsync(migration, DeployParameters.Default);

		// Assert
		await mockSet.MigrationItemDeployer.Received()
			.DeployAsync(1, Arg.Any<MigrationItem>());

		await mockSet.DbLiveDA.Received()
			.SetCurrentMigrationVersionAsync(migration.Version, new DateTime(2024, 1, 1));
	}

	private static FileData GetFileData(string relativePath, string content = "-- default item content")
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath,
			FilePath = "c:/data" + relativePath
		};
	}


	[Fact]
	public async Task DeployMigration_WithCustomSettings()
	{
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		string settingsJson = """
		{
		  "TransactionWrapLevel": "none",
		  "TransactionIsolationLevel": "ReadCommitted",
		  "MigrationTimeout": "02:30:00"
		}		
		""";

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.Settings] = new()
				{
					MigrationItemType = MigrationItemType.Settings,
					FileData = GetFileData("custom-settings.json", settingsJson)
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		await deploy.DeployAsync(migration, DeployParameters.Default);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, Arg.Any<MigrationItem>());

		await mockSet.DbLiveDA.Received()
			.SetCurrentMigrationVersionAsync(migration.Version, new DateTime(2024, 1, 1));

		await mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransactionAsync(
				false,
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(2.5),
				Arg.Any<Func<Task>>()
			);
	}
}