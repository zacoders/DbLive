
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationVersionDeployerTests
{
	[Fact]
	public void DeployMigration_EmptyMigration()
	{
		var mockSet = new MockSet();

		var deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = []
		};

		Assert.Throws<InvalidOperationException>(() => deploy.DeployMigration(false, migration));
	}

	[Fact]
	public void DeployMigration()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem> {
				[MigrationItemType.Migration] = new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.Received(1)
			.DeployMigrationItem(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SaveCurrentMigrationVersion(migration.Version, new DateTime(2024, 1, 1));
	}

	[Fact]
	public void DeployMigration_SkipMigrationTypes()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem> {
				[MigrationItemType.Migration] = new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.Undo] = new() {
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				[MigrationItemType.Breaking] = new() {
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SaveCurrentMigrationVersion(migration.Version, new DateTime(2024, 1, 1));
	}

	[Fact]
	public void DeployMigration_SelfDeployTest()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem> {
				[MigrationItemType.Migration] = new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.Undo] = new() {
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				[MigrationItemType.Breaking] = new() {
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(true, migration);

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SetDbLiveVersion(migration.Version, new DateTime(2024, 1, 1));
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
	public void DeployMigration_WithCustomSettings()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();
		
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

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.Received(1)
			.DeployMigrationItem(Arg.Any<bool>(), 1, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SaveCurrentMigrationVersion(migration.Version, new DateTime(2024, 1, 1));

		mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransaction(
				false, 
				TranIsolationLevel.ReadCommitted, 
				TimeSpan.FromHours(2.5), 
				Arg.Any<Action>()
			);
	}
}