using DbLive.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class MigrationDeployerTests
{
	[Fact]
	public void DeployMigration_EmptyMigration()
	{
		var mockSet = new MockSet();

		var deploy = mockSet.CreateUsingMocks<MigrationDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/",
			Items = new List<MigrationItem>().AsReadOnly()
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

		var deploy = mockSet.CreateUsingMocks<MigrationDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/",
			Items = new List<MigrationItem> {
				new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item2.sql")
				}
			}.AsReadOnly()
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.Received(2)
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.EasyFlowDA.Received()
			.SaveMigration(migration.Version, migration.Name, new DateTime(2024, 1, 1));
	}

	[Fact]
	public void DeployMigration_SkipMigrationTypes()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/",
			Items = new List<MigrationItem> {
				new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				new() {
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				new() {
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}.AsReadOnly()
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(false, migration);

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.EasyFlowDA.Received()
			.SaveMigration(migration.Version, migration.Name, new DateTime(2024, 1, 1));
	}

	[Fact]
	public void DeployMigration_SelfDeployTest()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/",
			Items = new List<MigrationItem> {
				new() {
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				new() {
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				new() {
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}.AsReadOnly()
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2024, 1, 1));

		deploy.DeployMigration(true, migration);

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.MigrationItemDeployer.Received(2)
			.MarkAsSkipped(Arg.Any<bool>(), migration, Arg.Any<MigrationItem>());

		mockSet.EasyFlowDA.Received()
			.SetEasyFlowVersion(migration.Version, new DateTime(2024, 1, 1));
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