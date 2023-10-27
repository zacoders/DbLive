namespace EasyFlow.Deployers;

public class MigrationItemDeployer
{
	private static readonly ILogger Logger = Log.ForContext(typeof(MigrationsDeployer));

	private readonly IEasyFlowDA _da;

	private EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public MigrationItemDeployer(IEasyFlowDA easyFlowDA)
	{
		_da = easyFlowDA;
	}

	public void DeployMigrationItem(string sqlConnectionString, bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //todo: separate timeout for single migration
			() =>
			{
				Logger.Information("Migration {migrationType}", migrationItem.MigrationType);

				DateTime migrationStartedUtc = DateTime.UtcNow;

				string status = "";
				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;
				if (migrationItem.MigrationType.In(MigrationType.Migration, MigrationType.Data))
				{
					_da.ExecuteNonQuery(sqlConnectionString, migrationItem.FileData.Content);
					status = "applied";
					migrationAppliedUtc = DateTime.UtcNow;
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
						DateTime.UtcNow,
						migrationAppliedUtc,
						executionTimeMs
					);
				}

				Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationType, status);
			}
		);
	}
}
