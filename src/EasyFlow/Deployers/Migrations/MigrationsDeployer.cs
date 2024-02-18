using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class MigrationsDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		IMigrationDeployer _migrationDeployer
	)
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

		foreach (var migration in migrationsToApply)
		{
			_migrationDeployer.DeployMigration(isSelfDeploy, migration);
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
}
