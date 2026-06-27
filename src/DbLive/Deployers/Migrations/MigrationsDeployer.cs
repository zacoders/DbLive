namespace DbLive.Deployers.Migrations;

public class MigrationsDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationVersionDeployer _migrationVersionDeployer
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

		IOrderedEnumerable<Migration> migrationsToApply = await GetMigrationsToApplyAsync().ConfigureAwait(false);

		if (!migrationsToApply.Any())
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (Migration migration in migrationsToApply)
		{
			await _migrationVersionDeployer.DeployAsync(migration, parameters).ConfigureAwait(false);
		}
	}

	internal protected async Task<IOrderedEnumerable<Migration>> GetMigrationsToApplyAsync()
	{
		IEnumerable<Migration> allMigrations = await _project.GetMigrationsAsync().ConfigureAwait(false);

		HashSet<long> appliedVersions = (await _da.GetAppliedMigrationVersionsAsync().ConfigureAwait(false)).ToHashSet();

		_logger.Information("Applied migration versions in target database: {AppliedVersions}.", appliedVersions.Count == 0 ? "none" : string.Join(", ", appliedVersions.OrderBy(v => v)));

		IEnumerable<Migration> migrationsToApply = allMigrations.Where(m => !appliedVersions.Contains(m.Version));

		return migrationsToApply.OrderBy(m => m.Version);
	}
}
