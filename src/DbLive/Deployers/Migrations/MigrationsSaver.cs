
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

		foreach (Migration migration in migrationsToApply)
		{
			foreach ((MigrationItemType _, MigrationItem migrationItem) in migration.Items)
			{
				long? hash = await _da.GetMigrationHashAsync(migration.Version, migrationItem.MigrationItemType).ConfigureAwait(false);

				if (hash.HasValue)
				{
					if (hash.Value == migrationItem.FileData.ContentHash)
					{
						// skip save, already saved
						continue;
					}
					else
					{
						// todo: Throw exception or just log warning? Overwriting migration item is not a good practice.
						//		 Need to analyze use cases where this can happen.
						_logger.Warning("Migration item '{MigrationItemType}' for version {Version} has changed, saving new version.", migrationItem.MigrationItemType, migration.Version);
					}
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