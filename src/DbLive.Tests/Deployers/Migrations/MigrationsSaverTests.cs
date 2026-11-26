
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationsSaverTests
{
	[Fact]
	public async Task Save_logs_start_message()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		ProjectSnapshot project = ProjectSnapshots.Empty;
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		// Act
		await saver.SaveAsync(project);

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

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [migration] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash, MigrationItemStatus.Applied)
		]);

		// Act
		await saver.SaveAsync(project);

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

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [migration] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(2, MigrationItemType.Migration, fileData.ContentHash + 123, MigrationItemStatus.None)
		]);

		var now = new DateTime(2025, 1, 2);
		mockSet.TimeProvider.UtcNow().Returns(now);

		// Act
		await saver.SaveAsync(project);

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
				dto.ContentHash == fileData.ContentHash &&
				dto.CreatedUtc == now
			));
	}

	[Fact]
	public async Task Save_skips_not_applied_item_when_only_line_endings_differ()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		FileData fileData = GetFileData("/001.breaking.sql", "drop table t1;\ngo\n");
		Migration migration = NewMigration(
			1,
			new MigrationItem
			{
				MigrationItemType = MigrationItemType.Breaking,
				Name = "001.breaking.sql",
				FileData = fileData
			});

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [migration] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			// saved from a CRLF checkout by a DbLive version that hashed the raw text.
			NewDbItem(1, MigrationItemType.Breaking, LegacyContentHash.Of("drop table t1;\r\ngo\r\n"), MigrationItemStatus.None)
		]);

		// Act
		await saver.SaveAsync(project);

		// Assert
		await mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItemAsync(Arg.Any<MigrationItemSaveDto>());
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

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [migration] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash + 123, MigrationItemStatus.Applied)
		]);

		// Act
		await saver.SaveAsync(project);

		// Assert
		await mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItemAsync(Arg.Any<MigrationItemSaveDto>());
	}

	[Theory]
	[InlineData(nameof(MigrationItemType.Migration))]
	[InlineData(nameof(MigrationItemType.Breaking))]
	[InlineData(nameof(MigrationItemType.MigrationSettings))]
	[InlineData(nameof(MigrationItemType.BreakingSettings))]
	public async Task Save_saves_new_item_with_its_hash(string itemTypeName)
	{
		// MigrationItemType is internal: a public test method takes the name of the value.
		MigrationItemType itemType = Enum.Parse<MigrationItemType>(itemTypeName);

		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		var item = new MigrationItem
		{
			MigrationItemType = itemType,
			Name = "",
			FileData = GetFileData("/001.item", "item content")
		};

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1, item)] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		// Act
		await saver.SaveAsync(project);

		// Assert
		await mockSet.DbLiveDA.Received(1)
			.SaveMigrationItemAsync(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.Version == 1 &&
				dto.ItemType == itemType &&
				dto.Status == MigrationItemStatus.None &&
				dto.ContentHash == item.FileData.ContentHash &&
				dto.RelativePath == "/001.item"
			));
	}

	[Fact]
	public async Task Save_updates_changed_breaking_settings_while_breaking_is_not_applied()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		var settingsItem = new MigrationItem
		{
			MigrationItemType = MigrationItemType.BreakingSettings,
			Name = "",
			FileData = GetFileData("/001.breaking.settings.json", """{ "Timeout": "00:05:00" }""")
		};

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1, settingsItem)] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.BreakingSettings, settingsItem.FileData.ContentHash + 1, MigrationItemStatus.None)
		]);

		// Act
		await saver.SaveAsync(project);

		// Assert
		await mockSet.DbLiveDA.Received(1)
			.SaveMigrationItemAsync(Arg.Is<MigrationItemSaveDto>(dto =>
				dto.ItemType == MigrationItemType.BreakingSettings &&
				dto.ContentHash == settingsItem.FileData.ContentHash
			));
	}

	[Fact]
	public async Task Save_keeps_rows_of_items_deleted_from_the_project()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationsSaver saver = mockSet.CreateUsingMocks<MigrationsSaver>();

		var migrationItem = new MigrationItem
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "",
			FileData = GetFileData("/001.migration.sql", "migration content")
		};

		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1, migrationItem)] };
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, migrationItem.FileData.ContentHash, MigrationItemStatus.Applied),
			// 001.breaking.sql was saved by a release and deleted from the project before its breaking phase.
			NewDbItem(1, MigrationItemType.Breaking, 123, MigrationItemStatus.None)
		]);

		// Act
		await saver.SaveAsync(project);

		// Assert: the saver neither saves nor deletes rows of items the project does not have.
		await mockSet.DbLiveDA.DidNotReceive()
			.SaveMigrationItemAsync(Arg.Any<MigrationItemSaveDto>());
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
			RelativePath = relativePath
		};
	}
}
