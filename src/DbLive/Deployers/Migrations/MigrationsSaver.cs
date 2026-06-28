
namespace DbLive.Deployers.Migrations;

internal class MigrationsSaver(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		ITimeProvider _timeProvider
	) : IMigrationsSaver
{
	public async Task SaveAsync()
	{
		// By default saving all migrations, skip content for all except UNDO.
		// Options can be added to control what to save

		_logger.Information("Saving migration items.");

		IEnumerable<Migration> migrationsToApply = await _project.GetMigrationsAsync().ConfigureAwait(false);
		IReadOnlyCollection<MigrationItemDto> dbItems = await _da.GetMigrationsAsync().ConfigureAwait(false);
		Dictionary<(long Version, MigrationItemType ItemType), MigrationItemDto> dbItemsByKey =
			dbItems.ToDictionary(i => (i.Version, i.ItemType));

		foreach (Migration migration in migrationsToApply)
		{
			foreach ((MigrationItemType _, MigrationItem migrationItem) in migration.Items)
			{
				if (dbItemsByKey.TryGetValue((migration.Version, migrationItem.MigrationItemType), out MigrationItemDto? dbItem))
				{
					if (dbItem.ContentHash == migrationItem.FileData.ContentHash)
					{
						continue;
					}

					if (dbItem.Status == MigrationItemStatus.Applied)
					{
						continue;
					}

					_logger.Information(
						"Migration item '{MigrationItemType}' for version {Version} has changed, saving new version.",
						migrationItem.MigrationItemType,
						migration.Version
					);
				}

				await _da.SaveMigrationItemAsync(new MigrationItemSaveDto
				{
					Version = migration.Version,
					ItemType = migrationItem.MigrationItemType,
					Name = migrationItem.Name,
					RelativePath = migrationItem.FileData.RelativePath,
					Status = MigrationItemStatus.None,
					Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : null,
					ContentHash = migrationItem.FileData.ContentHash,
					CreatedUtc = _timeProvider.UtcNow()
				}).ConfigureAwait(false);
			}
		}
	}
}
