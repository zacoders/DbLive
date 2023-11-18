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
				Logger.Information("Migration {migrationType}", migrationItem.MigrationType);

				DateTime migrationStartedUtc = _timeProvider.UtcNow();

				string status = "";
				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;

				//todo: this method should be refactored or removed, due to this logic related to item type
				if (migrationItem.MigrationType.In(migrationItemTypesToApply))
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
					_da.SaveMigrationItemState(
						sqlConnectionString,
						migration.Version,
						migration.Name,
						migrationItem.MigrationType.ToString().ToLower(),
						migrationItem.FileData.Crc32Hash,
						status,
						_timeProvider.UtcNow(),
						migrationAppliedUtc,
						executionTimeMs
					);
				}

				Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationType, status);
			}
		);
	}
}
