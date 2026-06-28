namespace DbLive.Tests.Deployers.Migrations;

public class MigrationChecksumValidatorTests
{
	[Fact]
	public async Task ValidateAsync_skips_when_dblive_not_installed()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default);

		await mockSet.DbLiveDA.DidNotReceive().GetMigrationsAsync();
		await mockSet.DbLiveProject.DidNotReceive().GetMigrationsAsync();
	}

	[Fact]
	public async Task ValidateAsync_strict_throws_on_applied_mismatch()
	{
		MockSet mockSet = new();
		FileData fileData = GetFileData("/001.migration.sql", "current content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash + 1, MigrationItemStatus.Applied, "/001.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Migration, fileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		MigrationChecksumMismatchException ex = await Assert.ThrowsAsync<MigrationChecksumMismatchException>(
			() => validator.ValidateAsync(DeployParameters.Default)
		);

		Assert.Contains("version 1", ex.Message);
		Assert.Contains("/001.migration.sql", ex.Message);
		Assert.Contains("RepairMigrationChecksums", ex.Message);
	}

	[Fact]
	public async Task ValidateAsync_strict_passes_when_hash_matches()
	{
		MockSet mockSet = new();
		FileData fileData = GetFileData("/001.migration.sql", "content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash, MigrationItemStatus.Applied, "/001.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Migration, fileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default);
	}

	[Fact]
	public async Task ValidateAsync_strict_ignores_not_applied_mismatch()
	{
		MockSet mockSet = new();
		FileData fileData = GetFileData("/002.migration.sql", "content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(2, MigrationItemType.Migration, fileData.ContentHash + 1, MigrationItemStatus.None, "/002.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(2, NewMigrationItem(MigrationItemType.Migration, fileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default);
	}

	[Fact]
	public async Task ValidateAsync_warn_logs_but_does_not_throw()
	{
		MockSet mockSet = new();
		FileData fileData = GetFileData("/001.migration.sql", "content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			MigrationChecksumMode = MigrationChecksumMode.Warn
		});
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash + 1, MigrationItemStatus.Applied, "/001.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Migration, fileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default);

		mockSet.Logger.Received(1).Warning(
			"Applied migration item '{ItemType}' version {Version} ({RelativePath}) has changed since it was applied.",
			MigrationItemType.Migration,
			1L,
			"/001.migration.sql"
		);
	}

	[Fact]
	public async Task ValidateAsync_repair_updates_checksum()
	{
		MockSet mockSet = new();
		FileData fileData = GetFileData("/001.migration.sql", "content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, fileData.ContentHash + 1, MigrationItemStatus.Applied, "/001.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Migration, fileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default with { RepairMigrationChecksums = true });

		await mockSet.DbLiveDA.Received(1).RepairMigrationChecksumAsync(Arg.Is<MigrationChecksumRepairDto>(dto =>
			dto.Version == 1 &&
			dto.ItemType == MigrationItemType.Migration &&
			dto.ContentHash == fileData.ContentHash &&
			dto.Content == null &&
			dto.RelativePath == "/001.migration.sql"
		));
	}

	[Fact]
	public async Task ValidateAsync_repair_includes_undo_content()
	{
		MockSet mockSet = new();
		FileData undoFileData = GetFileData("/001.undo.sql", "undo content");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Undo, undoFileData.ContentHash + 1, MigrationItemStatus.Applied, "/001.undo.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Undo, undoFileData))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		await validator.ValidateAsync(DeployParameters.Default with { RepairMigrationChecksums = true });

		await mockSet.DbLiveDA.Received(1).RepairMigrationChecksumAsync(Arg.Is<MigrationChecksumRepairDto>(dto =>
			dto.ItemType == MigrationItemType.Undo &&
			dto.Content == "undo content"
		));
	}

	[Fact]
	public async Task ValidateAsync_strict_lists_all_mismatches_in_exception()
	{
		MockSet mockSet = new();
		FileData migration1 = GetFileData("/001.migration.sql", "migration 1");
		FileData migration2 = GetFileData("/002.migration.sql", "migration 2");

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			NewDbItem(1, MigrationItemType.Migration, migration1.ContentHash + 1, MigrationItemStatus.Applied, "/001.migration.sql"),
			NewDbItem(2, MigrationItemType.Migration, migration2.ContentHash + 1, MigrationItemStatus.Applied, "/002.migration.sql")
		]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			NewMigration(1, NewMigrationItem(MigrationItemType.Migration, migration1)),
			NewMigration(2, NewMigrationItem(MigrationItemType.Migration, migration2))
		]);

		MigrationChecksumValidator validator = mockSet.CreateUsingMocks<MigrationChecksumValidator>();

		MigrationChecksumMismatchException ex = await Assert.ThrowsAsync<MigrationChecksumMismatchException>(
			() => validator.ValidateAsync(DeployParameters.Default)
		);

		Assert.Contains("version 1", ex.Message);
		Assert.Contains("version 2", ex.Message);
		Assert.Contains("/001.migration.sql", ex.Message);
		Assert.Contains("/002.migration.sql", ex.Message);
	}

	private static Migration NewMigration(long version, params MigrationItem[] items)
	{
		var dict = new Dictionary<MigrationItemType, MigrationItem>();
		foreach (MigrationItem item in items)
		{
			dict[item.MigrationItemType] = item;
		}

		return new Migration { Version = version, Items = dict };
	}

	private static MigrationItem NewMigrationItem(MigrationItemType itemType, FileData fileData)
	{
		return new MigrationItem
		{
			MigrationItemType = itemType,
			Name = fileData.FileName,
			FileData = fileData
		};
	}

	private static MigrationItemDto NewDbItem(
		long version,
		MigrationItemType itemType,
		long contentHash,
		MigrationItemStatus status,
		string relativePath)
	{
		return new MigrationItemDto
		{
			Version = version,
			ItemType = itemType,
			Name = Path.GetFileName(relativePath),
			RelativePath = relativePath,
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
