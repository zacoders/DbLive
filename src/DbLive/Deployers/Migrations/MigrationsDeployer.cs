using DbLive.Adapter;

namespace DbLive.Deployers.Migrations;

public class MigrationsDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationDeployer _migrationDeployer
	) : IMigrationsDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationsDeployer));

	public void DeployMigrations(bool isSelfDeploy, DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		_logger.Information("Deploying migrations.");

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, parameters);

		if (migrationsToApply.Count() == 0)
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (var migration in migrationsToApply)
		{
			_migrationDeployer.DeployMigration(isSelfDeploy, migration);
		}
	}

	internal protected IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, DeployParameters parameters)
	{
		IEnumerable<Migration> migrationsToApply = _project.GetMigrations();

		if (_da.DbLiveInstalled())
		{
			if (isSelfDeploy)
			{
				int appliedVersion = _da.GetDbLiveVersion();
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
}
