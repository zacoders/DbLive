
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationsSaverTests
{
	[Fact]
	public async Task Save_logs_start_message()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		// Act
		await saver.SaveAsync();

		// Assert
		mockSet.Logger.Received(1)
			.Information("Saving migration items.");
	}

	[Fact]
	public async Task Save_skips_item_when_hash_matches()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		FileData fileData = GetFileData("/001.migration.sql", "some content");
		Migration migration = NewMigration(
			1,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Migration,
				Name = "001.migration.sql",
				FileData = fileData
			});

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash, MigrationItemStatus.Applied)
		]);

		// Act
		await saver.SaveAsync();

		// Assert
		await mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItemAsync(Arg.Any<MigrationItemSaveDto>());
	}

	[Fact]
	public async Task Save_logs_and_saves_when_hash_differs_for_not_applied_item()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		FileData fileData = GetFileData("/001.migration.sql", "some content");
		Migration migration = NewMigration(
			2,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Migration,
				Name = "002.migration.sql",
				FileData = fileData
			});

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(2, MigrationItemType.Migration, fileData.ContentHash + 123, MigrationItemStatus.None)
		]);

		var now = new DateTime(2025, 1, 2);
		mockSet.TimeProvider.UtcNow().Returns(now);

		// Act
		await saver.SaveAsync();

		// Assert
		mockSet.Logger.Received(1)
			.Information(
				"Migration item '{MigrationItemType}' for version {Version} has changed, saving new version.",
				MigrationItemType.Migration,
				2L
			);

		await mockSet.DbLiveDA.Received(1)
			.SaveMigrationItemAsync(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.Version == 2 &&
				dto.ItemType == MigrationItemType.Migration &&
				dto.Content == null &&
				dto.ContentHash == fileData.ContentHash &&
				dto.CreatedUtc == now
			));
	}

	[Fact]
	public async Task Save_skips_applied_item_when_hash_differs()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		FileData fileData = GetFileData("/001.migration.sql", "some content");
		Migration migration = NewMigration(
			1,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Migration,
				Name = "001.migration.sql",
				FileData = fileData
			});

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash + 123, MigrationItemStatus.Applied)
		]);

		// Act
		await saver.SaveAsync();

		// Assert
		await mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItemAsync(Arg.Any<MigrationItemSaveDto>());
	}

	[Fact]
	public async Task Save_saves_content_only_for_undo_items()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

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

		Migration migration = NewMigration(1, undoItem, migrationItem);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		// Act
		await saver.SaveAsync();

		// Assert
		await mockSet.DbLiveDA.Received(1)
			.SaveMigrationItemAsync(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.ItemType == MigrationItemType.Undo &&
				dto.Content == "undo content"
			));

		await mockSet.DbLiveDA.Received(1)
			.SaveMigrationItemAsync(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.ItemType == MigrationItemType.Migration &&
				dto.Content == null
			));
	}


	static Migration NewMigration(
		long version,
		params MigrationItem[] items)
	{
		var dict = new Dictionary<MigrationItemType, MigrationItem>();
		foreach (MigrationItem item in items)
		{
			dict[item.MigrationItemType] = item;
		}

		return new Migration
		{
			Version = version,
			Items = dict
		};
	}

	private static MigrationItemDto NewDbItem(
		long version,
		MigrationItemType itemType,
		long contentHash,
		MigrationItemStatus status)
	{
		return new MigrationItemDto
		{
			Version = version,
			ItemType = itemType,
			Name = $"{version}.{itemType}.sql",
			RelativePath = $"/{version}.{itemType}.sql",
			ContentHash = contentHash,
			Status = status
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
