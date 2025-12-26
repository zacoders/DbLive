using DbLive.Adapter;
using DbLive.Common.Settings;

namespace DbLive.Deployers.Migrations;

public class MigrationVersionDeployer(
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IMigrationVersionDeployer
{
	
	private readonly DbLiveSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void DeployMigration(bool isSelfDeploy, Migration migration)
	{
		//_logger.Information("Deploying migration v{version}", migration.Version);

		if (migration.Items.Count == 0) return;

		if (migration.Items.ContainsKey(MigrationItemType.Settings))
		{
			MigrationItem settingsItem = migration.Items[MigrationItemType.Settings];
			// todo: apply settings
		}

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
