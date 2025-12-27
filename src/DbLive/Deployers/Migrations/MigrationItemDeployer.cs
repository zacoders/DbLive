namespace DbLive.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	public void DeployMigrationItem(bool isSelfDeploy, int migrationVersion, MigrationItem migrationItem)
	{
		_transactionRunner.ExecuteWithinTransaction(
			false,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.MigrationTimeout,
			() =>
			{
				_logger.Information(
					"Deploying {relativePath}. Type {migrationType}.",
					migrationItem.FileData.RelativePath,
					migrationItem.MigrationItemType
				);

				DateTime migrationStartedUtc = _timeProvider.UtcNow();

				DateTime? migrationAppliedUtc = null;
				long? executionTimeMs = null;

				_da.ExecuteNonQuery(
					migrationItem.FileData.Content,
					_projectSettings.TransactionIsolationLevel,
					_projectSettings.MigrationTimeout
				);

				migrationAppliedUtc = _timeProvider.UtcNow();
				executionTimeMs = (long)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;

				if (!isSelfDeploy)
				{
					string content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "";

					DateTime createdUtc = _timeProvider.UtcNow();

					int crc32Hash = migrationItem.FileData.Crc32Hash;

					MigrationItemDto dto = new()
					{
						Version = migrationVersion,
						Name = migrationItem.Name,
						ItemType = migrationItem.MigrationItemType,
						ContentHash = crc32Hash,
						Content = content,
						Status = MigrationItemStatus.Applied,
						CreatedUtc = createdUtc,
						AppliedUtc = migrationAppliedUtc,
						ExecutionTimeMs = executionTimeMs
					};

					_da.SaveMigrationItemState(dto);
				}
			}
		);
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
