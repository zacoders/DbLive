using DbLive.Deployers.Migrations;

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

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.DidNotReceive()
			.MarkAsSkipped(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());
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
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SaveMigration(migration.Version, new DateTime(2024, 1, 1));
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
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SaveMigration(migration.Version, new DateTime(2024, 1, 1));
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
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.Received()
			.SetDbLiveVersion(migration.Version, new DateTime(2024, 1, 1));
	}

	private static FileData GetFileData(string relativePath)
	{
		return new FileData
		{
			Content = $"-- item content",
			RelativePath = relativePath,
			FilePath = "c:/data" + relativePath
		};
	}
}