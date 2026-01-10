
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationsSaverTests
{
	[Fact]
	public void Save_logs_start_message()
	{
		// Arrange
		MockSet mockSet = new();
		var saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([]);

		// Act
		saver.SaveAsync();

		// Assert
		mockSet.Logger.Received(1)
			.Information("Saving migration items.");
	}

	[Fact]
	public void Save_skips_item_when_hash_matches()
	{
		// Arrange
		MockSet mockSet = new();
		var saver = mockSet.CreateUsingMocks<MigrationsSaver>();
		
		FileData fileData = GetFileData("/001.migration.sql", "some content");
		var migration = NewMigration(
			1,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Migration,
				Name = "001.migration.sql",
				FileData = fileData
			});

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationHash(1, MigrationItemType.Migration)
			.Returns(fileData.ContentHash);

		// Act
		saver.SaveAsync();

		// Assert
		mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItem(Arg.Any<MigrationItemSaveDto>());

		mockSet.Logger.DidNotReceive()
			.Warning(Arg.Any<string>(), Arg.Any<object[]>());
	}

	[Fact]
	public void Save_logs_warning_and_saves_when_hash_differs()
	{
		// Arrange
		MockSet mockSet = new();
		var saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		FileData fileData = GetFileData("/001.migration.sql", "some content");
		var migration = NewMigration(
			2,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Migration,
				Name = "002.migration.sql",
				FileData = fileData
			});

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);

		mockSet.DbLiveDA.GetMigrationHash(2, MigrationItemType.Migration)
			.Returns(fileData.ContentHash + 123); // different hash

		var now = new DateTime(2025, 1, 2);
		mockSet.TimeProvider.UtcNow().Returns(now);

		// Act
		saver.SaveAsync();

		// Assert
		mockSet.Logger.Received(1)
			.Warning(
				"Migration item '{MigrationItemType}' for version {Version} has changed, saving new version.",
				MigrationItemType.Migration,
				2
			);

		mockSet.DbLiveDA.Received(1)
			.SaveMigrationItem(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.Version == 2 &&
				dto.ItemType == MigrationItemType.Migration &&
				dto.Content == null &&
				dto.ContentHash == fileData.ContentHash &&
				dto.CreatedUtc == now
			));
	}

	[Fact]
	public void Save_saves_content_only_for_undo_items()
	{
		// Arrange
		MockSet mockSet = new();
		var saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		var undoItem = new MigrationItem
		{
			MigrationItemType = MigrationItemType.Undo,
			Name = "001.undo.sql",
			FileData = GetFileData("/001.undo.sql", "undo content")
		};

		var migrationItem = new MigrationItem
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "001.migration.sql",
			FileData = GetFileData("/001.migration.sql", "migration content")
		};

		var migration = NewMigration(1, undoItem, migrationItem);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationHash(Arg.Any<int>(), Arg.Any<MigrationItemType>())
			.Returns((int?)null);

		// Act
		saver.SaveAsync();

		// Assert
		mockSet.DbLiveDA.Received(1)
			.SaveMigrationItem(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.ItemType == MigrationItemType.Undo &&
				dto.Content == "undo content"
			));

		mockSet.DbLiveDA.Received(1)
			.SaveMigrationItem(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.ItemType == MigrationItemType.Migration &&
				dto.Content == null
			));
	}


	static Migration NewMigration(
		int version,
		params MigrationItem[] items)
	{
		var dict = new Dictionary<MigrationItemType, MigrationItem>();
		foreach (var item in items)
		{
			dict[item.MigrationItemType] = item;
		}

		return new Migration
		{
			Version = version,
			Items = dict
		};
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