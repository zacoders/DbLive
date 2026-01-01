namespace DbLive.Deployers.Migrations;

public class MigrationsDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationVersionDeployer _migrationVersionDeployer
	) : IMigrationsDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationsDeployer));

	public void DeployMigrations(DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		_logger.Information("Deploying migrations.");

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply();

		if (migrationsToApply.Count() == 0)
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (var migration in migrationsToApply)
		{
			_migrationVersionDeployer.DeployMigration(migration, parameters);
		}
	}

	internal protected IOrderedEnumerable<Migration> GetMigrationsToApply()
	{
		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		var appliedVersion = _da.GetCurrentMigrationVersion();

		_logger.Information("Current migration version in target database: {AppliedVersion}.", appliedVersion);

		migrationsToApply = migrationsToApply.Where(m => m.Version > appliedVersion);

		return migrationsToApply.OrderBy(m => m.Version);
	}
}
