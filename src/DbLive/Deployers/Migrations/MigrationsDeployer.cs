namespace DbLive.Deployers.Migrations;

internal class MigrationsDeployer(
		ILogger _logger,
		IDbLiveInternalDA _da,
		IMigrationVersionDeployer _migrationVersionDeployer
	) : IMigrationsDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationsDeployer));

	public async Task DeployAsync(ProjectSnapshot project, DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		_logger.Information("Deploying migrations.");

		IOrderedEnumerable<Migration> migrationsToApply = await GetMigrationsToApplyAsync(project).ConfigureAwait(false);

		if (!migrationsToApply.Any())
		{
			_logger.Information("No migrations to apply.");
			return;
		}

		foreach (Migration migration in migrationsToApply)
		{
			await _migrationVersionDeployer.DeployAsync(migration).ConfigureAwait(false);
		}
	}

	internal protected async Task<IOrderedEnumerable<Migration>> GetMigrationsToApplyAsync(ProjectSnapshot project)
	{
		HashSet<long> appliedVersions = (await _da.GetAppliedMigrationVersionsAsync().ConfigureAwait(false)).ToHashSet();

		_logger.Information("Applied migration versions in target database: {AppliedVersions}.", appliedVersions.Count == 0 ? "none" : string.Join(", ", appliedVersions.OrderBy(v => v)));

		IEnumerable<Migration> migrationsToApply = project.Migrations.Where(m => !appliedVersions.Contains(m.Version));

		return migrationsToApply.OrderBy(m => m.Version);
	}
}
