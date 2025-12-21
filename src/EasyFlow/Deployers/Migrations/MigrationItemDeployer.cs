using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger _logger,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	private readonly EasyFlowSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	public void DeployMigrationItem(bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		_transactionRunner.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.MigrationItem,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.MigrationItemTimeout,
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

				_da.ExecuteNonQuery(migrationItem.FileData.Content);

				migrationAppliedUtc = _timeProvider.UtcNow();
				executionTimeMs = (long)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;

				if (!isSelfDeploy)
				{
					string content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "";

					DateTime createdUtc = _timeProvider.UtcNow();

					int crc32Hash = migrationItem.FileData.Crc32Hash;

					MigrationItemDto dto = new()
					{
						Version = migration.Version,
						Name = migration.Name,
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
