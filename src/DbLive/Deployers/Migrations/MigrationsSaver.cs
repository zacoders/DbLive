
namespace DbLive.Deployers.Migrations;

internal class MigrationsSaver(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		ITimeProvider _timeProvider
	) : IMigrationsSaver
{
	public void Save()
	{
		// By default saving all migrations, skip content for all except UNDO.
		// Options can be added to control what to save

		_logger.Information("Saving migration undo items.");

		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		foreach (Migration migration in migrationsToApply)
		{
			foreach ((var _, MigrationItem migrationItem) in migration.Items)
			{
				int? hash = _da.GetMigrationHash(migration.Version, migrationItem.MigrationItemType);

				if (hash.HasValue && hash.Value == migrationItem.FileData.ContentHash)
				{
					// skip save, already saved
					continue;
				}

				_da.SaveMigrationItem(new MigrationItemSaveDto
				{
					Version = migration.Version,
					ItemType = migrationItem.MigrationItemType,
					Name = migrationItem.Name,
					Status = MigrationItemStatus.None,
					Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : null,
					ContentHash = migrationItem.FileData.ContentHash,
					CreatedUtc = _timeProvider.UtcNow()
				});
			}
		}
	}
}