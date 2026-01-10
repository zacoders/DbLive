namespace DbLive.Deployers.Migrations;

public class MigrationsDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationVersionDeployer _migrationVersionDeployer,
		IMigrationsSaver _migrationsSaver
	) : IMigrationsDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationsDeployer));

	public async Task DeployAsync(DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		_logger.Information("Deploying migrations.");

		// saving migrations before deploying
		await _migrationsSaver.SaveAsync();

		IOrderedEnumerable<Migration> migrationsToApply = await GetMigrationsToApplyAsync();

		if (!migrationsToApply.Any())
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (var migration in migrationsToApply)
		{
			await _migrationVersionDeployer.DeployAsync(migration, parameters);
		}
	}

	internal protected async Task<IOrderedEnumerable<Migration>> GetMigrationsToApplyAsync()
	{
		IEnumerable<Migration> migrationsToApply = await _project.GetMigrationsAsync();

		int appliedVersion = await _da.GetCurrentMigrationVersionAsync();

		_logger.Information("Current migration version in target database: {AppliedVersion}.", appliedVersion);

		migrationsToApply = migrationsToApply.Where(m => m.Version > appliedVersion);

		return migrationsToApply.OrderBy(m => m.Version);
	}
}
