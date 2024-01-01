namespace EasyFlow.Deployers;

public class MigrationsDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		MigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider
	)
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationsDeployer));

	private readonly EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public void DeployMigrations(bool isSelfDeploy, DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		_logger.Information("Deploying migrations.");

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, parameters);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(isSelfDeploy, migration);
		}
	}

	internal protected IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, DeployParameters parameters)
	{
		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		if (_da.EasyFlowInstalled())
		{
			if (isSelfDeploy)
			{
				int appliedVersion = _da.GetEasyFlowVersion();
				migrationsToApply = migrationsToApply
					.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
					.Where(m => m.Version > appliedVersion);
			}
			else
			{
				var appliedMigrations = _da.GetMigrations();
				migrationsToApply = migrationsToApply
					.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
					.Where(m => !appliedMigrations.Any(am => am.Version == m.Version && am.Name == m.Name));
			}
		}

		return migrationsToApply
				.OrderBy(m => m.Version)
				.ThenBy(m => m.Name);
	}

	internal protected void DeployMigration(bool isSelfDeploy, Migration migration)
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
