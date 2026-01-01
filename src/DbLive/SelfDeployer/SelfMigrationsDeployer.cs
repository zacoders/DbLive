namespace DbLive.SelfDeployer;

public class SelfMigrationsDeployer(
		ILogger _logger,
		InternalDbLiveProject _project,
		ITimeProvider _timeProvider,
		IDbLiveDA _da
	) : ISelfMigrationsDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(SelfMigrationsDeployer));

	public void DeployMigrations()
	{
		_logger.Information("Deploying migrations.");

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply();

		if (migrationsToApply.Count() == 0)
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(migration);
		}
	}

	internal protected IOrderedEnumerable<Migration> GetMigrationsToApply()
	{
		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		if (_da.DbLiveInstalled())
		{
			int appliedVersion = _da.GetDbLiveVersion();
			migrationsToApply = migrationsToApply.Where(m => m.Version > appliedVersion);			
		}

		return migrationsToApply.OrderBy(m => m.Version);
	}

	internal void DeployMigration(Migration migration)
	{
		var migrationItem = migration.Items[MigrationItemType.Migration];
		
		DeployMigrationItem(migrationItem);

		DateTime migrationCompletedUtc = _timeProvider.UtcNow();

		_da.SetDbLiveVersion(migration.Version, migrationCompletedUtc);		
	}

	public void DeployMigrationItem(MigrationItem migrationItem)
	{		
		try
		{
			_logger.Information(
				"Deploying {relativePath}. Type {migrationType}.",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType
			);

			_da.ExecuteNonQuery(
				migrationItem.FileData.Content,
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromMinutes(5)
			);
		}
		catch (Exception ex)
		{
			throw new MigrationDeploymentException($"Migration file deployment error. File path: {migrationItem.FileData.RelativePath}", ex);
		}
	}
}
