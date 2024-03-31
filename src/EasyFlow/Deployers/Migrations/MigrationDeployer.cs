using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationDeployer(
		ILogger _logger,
		IEasyFlowDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IMigrationDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationDeployer));

	private readonly EasyFlowSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void DeployMigration(bool isSelfDeploy, Migration migration)
	{
		_logger.Information("Applying migration: {path}", migration.FolderPath.GetLastSegment());

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
					_da.SetEasyFlowVersion(migration.Version, migrationCompletedUtc);
				}
				else
				{
					_da.SaveMigration(migration.Version, migration.Name, migrationCompletedUtc);
				}
			}
		);
	}
}
