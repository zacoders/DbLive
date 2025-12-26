using DbLive.Adapter;

namespace DbLive.Deployers.Migrations;

public class MigrationVersionDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IMigrationVersionDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationVersionDeployer));

	private readonly DbLiveSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void DeployMigration(bool isSelfDeploy, Migration migration)
	{
		_logger.Information("Deploying migration v{version}", migration.Version);

		if (migration.Items.Count == 0) return;

		_transactionRunner.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.MigrationTimeout,
			() =>
			{
				foreach (MigrationItem migrationItem in migration.Items.Select(t => t.Value).OrderBy(t => t.MigrationItemType))
				{
					if (migrationItem.MigrationItemType == MigrationItemType.Migration)
					{
						_migrationItemDeployer.DeployMigrationItem(isSelfDeploy, migration.Version, migrationItem);
					}
					else
					{
						_migrationItemDeployer.MarkAsSkipped(isSelfDeploy, migration.Version, migrationItem);
					}
				}

				DateTime migrationCompletedUtc = _timeProvider.UtcNow();

				if (isSelfDeploy)
				{
					_da.SetDbLiveVersion(migration.Version, migrationCompletedUtc);
				}
				else
				{
					_da.SaveCurrentMigrationVersion(migration.Version, migrationCompletedUtc);
				}
			}
		);
	}
}
