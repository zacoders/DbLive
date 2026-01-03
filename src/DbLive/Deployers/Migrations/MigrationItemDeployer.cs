
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

	public void Deploy(int migrationVersion, MigrationItem migrationItem)
	{
		DateTime startTimeUtc = _timeProvider.UtcNow();
		
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

			DateTime migrationEndTime = _timeProvider.UtcNow();
			MigrationItemStateDto dto = new()
			{
				Version = migrationVersion,
				ItemType = migrationItem.MigrationItemType,
				Status = MigrationItemStatus.Applied,
				AppliedUtc = migrationEndTime,
				ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds,
				ErrorMessage = null
			};
			_da.UpdateMigrationState(dto);

			if (migrationItem.MigrationItemType == MigrationItemType.Undo)
			{
				UpdateDateForRevertedMigrations(migrationVersion);
			}

			if (migrationItem.MigrationItemType == MigrationItemType.Migration)
			{
				if (_da.MigrationItemExists(migrationVersion, MigrationItemType.Undo))
				{
					MigrationItemStateDto breakingDto = new()
					{
						Version = migrationVersion,
						ItemType = MigrationItemType.Undo,
						Status = MigrationItemStatus.None,
						AppliedUtc = null,
						ExecutionTimeMs = null,
						ErrorMessage = null
					};
					_da.UpdateMigrationState(breakingDto);
				}
			}
		}
		catch (Exception ex)
		{
			DateTime migrationEndTime = _timeProvider.UtcNow();
			MigrationItemStateDto dto = new()
			{
				Version = migrationVersion,
				ItemType = migrationItem.MigrationItemType,
				Status = MigrationItemStatus.Failed,
				AppliedUtc = migrationEndTime,
				ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds,
				ErrorMessage = ex.ToString()
			};
			_da.UpdateMigrationState(dto); // todo: it will be missed if external transaction fail.
			throw new MigrationDeploymentException($"Migration file deployment error. File path: {migrationItem.FileData.RelativePath}", ex);
		}
	}

	private void UpdateDateForRevertedMigrations(int migrationVersion)
	{
		MigrationItemStateDto migrationDto = new()
		{
			Version = migrationVersion,
			ItemType = MigrationItemType.Migration,
			Status = MigrationItemStatus.Reverted,
			AppliedUtc = null,
			ExecutionTimeMs = null,
			ErrorMessage = null
		};
		_da.UpdateMigrationState(migrationDto);

		if (_da.MigrationItemExists(migrationVersion, MigrationItemType.Breaking))
		{
			MigrationItemStateDto breakingDto = new()
			{
				Version = migrationVersion,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.Reverted,
				AppliedUtc = null,
				ExecutionTimeMs = null,
				ErrorMessage = null
			};
			_da.UpdateMigrationState(breakingDto);
		}		
	}
}
