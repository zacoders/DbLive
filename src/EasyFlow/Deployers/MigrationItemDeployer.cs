namespace EasyFlow.Deployers;

public class MigrationItemDeployer(IEasyFlowDA _da, ITimeProvider _timeProvider)
{
	private static readonly ILogger Logger = Log.ForContext(typeof(MigrationsDeployer));

	private EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public void DeployMigrationItem(string sqlConnectionString, bool isSelfDeploy, Migration migration, MigrationItem migrationItem, MigrationItemType[] migrationItemTypesToApply)
	{
		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.MigrationItem,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //todo: separate timeout for single migration
			() =>
			{
				Logger.Information("Migration {migrationType}", migrationItem.MigrationItemType);

				DateTime migrationStartedUtc = _timeProvider.UtcNow();

				string status = "";
				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;

				//todo: this method should be refactored or removed, due to this logic related to item type
				if (migrationItem.MigrationItemType.In(migrationItemTypesToApply))
				{
					_da.ExecuteNonQuery(sqlConnectionString, migrationItem.FileData.Content);
					status = "applied";
					migrationAppliedUtc = _timeProvider.UtcNow();
					executionTimeMs = (int)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;
				}
				else
				{
					status = "skipped";
				}

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

					_da.SaveMigrationItemState(sqlConnectionString, dto);
				}

				Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationItemType, status);
			}
		);
	}
}
