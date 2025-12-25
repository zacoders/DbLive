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
		_logger.Information("Deploying migration: {path}", migration.FolderPath.GetLastSegment());

		if (migration.Items.Count == 0) return;

		_transactionRunner.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.MigrationTimeout,
			() =>
			{
				foreach (MigrationItem migrationItem in migration.Items.OrderBy(t => t.MigrationItemType))
				{
					if (migrationItem.MigrationItemType == MigrationItemType.Migration)
					{
						_migrationItemDeployer.DeployMigrationItem(isSelfDeploy, migration, migrationItem);
					}
					else
					{
						_migrationItemDeployer.MarkAsSkipped(isSelfDeploy, migration, migrationItem);
					}
				}

				DateTime migrationCompletedUtc = _timeProvider.UtcNow();

				if (isSelfDeploy)
				{
					_da.SetDbLiveVersion(migration.Version, migrationCompletedUtc);
				}
				else
				{
					_da.SaveMigration(migration.Version, migration.Name, migrationCompletedUtc);
				}
			}
		);
	}
}
