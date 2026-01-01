
namespace DbLive.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	public void DeployMigrationItem(bool isSelfDeploy, int migrationVersion, MigrationItem migrationItem)
	{
		DateTime startTimeUtc = _timeProvider.UtcNow();
		MigrationItemStatus? migrationStatus = null;

		try
		{
			_logger.Information(
				"Deploying {relativePath}. Type {migrationType}.",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType
			);

			_da.ExecuteNonQuery(
				migrationItem.FileData.Content,
				_projectSettings.TransactionIsolationLevel,
				_projectSettings.MigrationTimeout
			);

			migrationStatus = MigrationItemStatus.Applied;
		}
		catch (Exception ex)
		{
			migrationStatus = MigrationItemStatus.Failed;
			throw new MigrationDeploymentException($"Migration file deployment error. File path: {migrationItem.FileData.RelativePath}", ex);
		}
		finally
		{
			if (!isSelfDeploy)
			{
				DateTime migrationEndTime = _timeProvider.UtcNow();
				MigrationItemDto dto = new()
				{
					Version = migrationVersion,
					Name = migrationItem.Name,
					ItemType = migrationItem.MigrationItemType,
					ContentHash = migrationItem.FileData.Crc32Hash,
					Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "",
					Status = migrationStatus!.Value,
					CreatedUtc = _timeProvider.UtcNow(),
					AppliedUtc = migrationStatus == MigrationItemStatus.Applied ? migrationEndTime : null,
					ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds

				};
				_da.SaveMigrationItemState(dto);
			}
		}
	}

	public void MarkAsSkipped(bool isSelfDeploy, int migrationVersion, MigrationItem migrationItem)
	{
		//_logger.Information("Migration {migrationType}", migrationItem.MigrationItemType);

		if (!isSelfDeploy)
		{
			MigrationItemDto dto = new()
			{
				Version = migrationVersion,
				Name = migrationItem.Name,
				ItemType = migrationItem.MigrationItemType,
				ContentHash = migrationItem.FileData.Crc32Hash,
				Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "",
				Status = MigrationItemStatus.Skipped,
				CreatedUtc = _timeProvider.UtcNow(),
				AppliedUtc = null,
				ExecutionTimeMs = null
			};

			_da.SaveMigrationItemState(dto);
		}

		_logger.Information("Migration v{migrationVersion} {migrationType} skipped.", migrationVersion, migrationItem.MigrationItemType);
	}
}
