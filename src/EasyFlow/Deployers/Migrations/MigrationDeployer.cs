using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		MigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider
	) : IMigrationDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationDeployer));

	private readonly EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public void DeployMigration(bool isSelfDeploy, Migration migration)
	{
		_logger.Information("Applying migration: {path}", migration.FolderPath.GetLastSegment());
		var migrationItems = _project.GetMigrationItems(migration.FolderPath);

		if (migrationItems.Count == 0) return;

		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //toto: separate timeout for all migrations
			() =>
			{
				foreach (MigrationItem migrationItem in migrationItems.OrderBy(t => t.MigrationItemType))
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
