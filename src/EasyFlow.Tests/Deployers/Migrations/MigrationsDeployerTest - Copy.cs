using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class MigrationDeployerTest
{
	[Fact]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		var deploy = new MigrationDeployer(mockSet.Logger, mockSet.EasyFlowDA, mockSet.MigrationItemDeployer, mockSet.TimeProvider);

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