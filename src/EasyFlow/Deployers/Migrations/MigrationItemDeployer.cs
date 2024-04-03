using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger logger,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = logger.ForContext(typeof(MigrationItemDeployer));

	private readonly EasyFlowSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void DeployMigrationItem(bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		_transactionRunner.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.MigrationItem,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.MigrationTimeout,
			() =>
			{
				_logger.Information(
					"Migration {migrationType}, {relativePath}.",
					migrationItem.MigrationItemType,
					migrationItem.FileData.RelativePath
				);

				DateTime migrationStartedUtc = _timeProvider.UtcNow();

				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;

				_da.ExecuteNonQuery(migrationItem.FileData.Content);
				
				migrationAppliedUtc = _timeProvider.UtcNow();
				executionTimeMs = (int)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;

				if (!isSelfDeploy)
				{
					MigrationItemDto dto = new()
					{
						Version = migration.Version,
						Name = migration.Name,
						ItemType = migrationItem.MigrationItemType,
						ContentHash = migrationItem.FileData.Crc32Hash,
						Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "",
						Status = MigrationItemStatus.Applied,
						CreatedUtc = _timeProvider.UtcNow(),
						AppliedUtc = migrationAppliedUtc,
						ExecutionTimeMs = executionTimeMs
					};

					_da.SaveMigrationItemState(dto);
				}

				_logger.Information("Migration {migrationType} applied.", migrationItem.MigrationItemType);
			}
		);
	}

	public void MarkAsSkipped(bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		_logger.Information("Migration {migrationType}", migrationItem.MigrationItemType);

		if (!isSelfDeploy)
		{
			MigrationItemDto dto = new()
			{
				Version = migration.Version,
				Name = migration.Name,
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

		_logger.Information("Migration {migrationType} skipped.", migrationItem.MigrationItemType);
	}
}
