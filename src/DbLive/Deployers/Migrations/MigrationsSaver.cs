
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
		// a row for every migration item of the project, with its hash, not its content: the project is the source of the scripts.
		// rows of items deleted from the project stay. nothing runs them: the migration and breaking phases and the checksum
		// validation take the scripts and their settings from the project and skip a row the project has no item for.

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
					if (migrationItem.FileData.MatchesHash(dbItem.ContentHash))
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
					ContentHash = migrationItem.FileData.ContentHash,
					CreatedUtc = _timeProvider.UtcNow()
				}).ConfigureAwait(false);
			}
		}
	}
}
