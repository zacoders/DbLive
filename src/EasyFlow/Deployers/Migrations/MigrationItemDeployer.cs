using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationItemDeployer(ILogger _logger, IEasyFlowDA _da, ITimeProvider _timeProvider)
{
	private readonly ILogger Logger = _logger.ForContext(typeof(MigrationItemDeployer));

	private readonly EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public void DeployMigrationItem(bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.MigrationItem,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //todo: separate timeout for single migration
			() =>
			{
				Logger.Information(
					"Migration {migrationType}, {relativePath}.",
					migrationItem.MigrationItemType,
					migrationItem.FileData.RelativePath
				);

				DateTime migrationStartedUtc = _timeProvider.UtcNow();

				string status = "";
				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;

				_da.ExecuteNonQuery(migrationItem.FileData.Content);
				status = "applied";
				migrationAppliedUtc = _timeProvider.UtcNow();
				executionTimeMs = (int)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;

				if (!isSelfDeploy)
				{
					MigrationItemDto dto = new()
					{
						Version = migration.Version,
						Name = migration.Name,
						ItemType = migrationItem.MigrationItemType.ToString().ToLower(),
						ContentHash = migrationItem.FileData.Crc32Hash,
						Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "",
						Status = status,
						CreatedUtc = _timeProvider.UtcNow(),
						AppliedUtc = migrationAppliedUtc,
						ExecutionTimeMs = executionTimeMs
					};

					_da.SaveMigrationItemState(dto);
				}

				Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationItemType, status);
			}
		);
	}

	public void MarkAsSkipped(bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		Logger.Information("Migration {migrationType}", migrationItem.MigrationItemType);

		string status = "skipped";
		if (!isSelfDeploy)
		{
			MigrationItemDto dto = new()
			{
				Version = migration.Version,
				Name = migration.Name,
				ItemType = migrationItem.MigrationItemType.ToString().ToLower(),
				ContentHash = migrationItem.FileData.Crc32Hash,
				Content = migrationItem.MigrationItemType == MigrationItemType.Undo ? migrationItem.FileData.Content : "",
				Status = status,
				CreatedUtc = _timeProvider.UtcNow(),
				AppliedUtc = null,
				ExecutionTimeMs = null
			};

			_da.SaveMigrationItemState(dto);
		}

		Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationItemType, status);
	}
}
