namespace EasyFlow.Deployers;

public class MigrationsDeployer(
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		MigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider
	)
{
	private static readonly ILogger Logger = Log.ForContext(typeof(MigrationsDeployer));

	private EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public void DeployMigrations(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, sqlConnectionString, parameters);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(isSelfDeploy, migration, sqlConnectionString);
		}
	}

	internal protected IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		if (_da.EasyFlowInstalled(sqlConnectionString))
		{
			if (isSelfDeploy)
			{
				int appliedVersion = _da.GetEasyFlowVersion(sqlConnectionString);
				migrationsToApply = migrationsToApply
					.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
					.Where(m => m.Version > appliedVersion);
			}
			else
			{
				var appliedMigrations = _da.GetMigrations(sqlConnectionString);
				migrationsToApply = migrationsToApply
					.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
					.Where(m => !appliedMigrations.Any(am => am.Version == m.Version && am.Name == m.Name));
			}
		}

		return migrationsToApply
				.OrderBy(m => m.Version)
				.ThenBy(m => m.Name);
	}

	internal protected void DeployMigration(bool isSelfDeploy, Migration migration, string sqlConnectionString)
	{
		Logger.Information(migration.FolderPath.GetLastSegment());
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
					_migrationItemDeployer.DeployMigrationItem(sqlConnectionString, isSelfDeploy, migration, migrationItem, new[] { MigrationItemType.Migration, MigrationItemType.Data });
				}

				DateTime migrationCompletedUtc = _timeProvider.UtcNow();

				if (isSelfDeploy)
				{
					_da.SetEasyFlowVersion(sqlConnectionString, migration.Version, migrationCompletedUtc);
				}
				else
				{
					_da.SaveMigration(sqlConnectionString, migration.Version, migration.Name, migrationCompletedUtc);
				}
			}
		);
	}
}
