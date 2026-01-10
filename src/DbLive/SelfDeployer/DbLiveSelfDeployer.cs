namespace DbLive.SelfDeployer;

internal class DbLiveSelfDeployer(
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		IInternalDbLiveProject _internalProject,
		IDbLiveDA _da,
		ITimeProvider _timeProvider
	) : IDbLiveSelfDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(DbLiveSelfDeployer));

	public async Task DeployAsync()
	{
		DbLiveSettings projectSettings = await _projectSettings.GetProjectSettingsAsync().ConfigureAwait(false);

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Starting self deploy.");
		}

		IEnumerable<InternalMigration> migrationsToApply = await _internalProject.GetMigrationsAsync().ConfigureAwait(false);

		if (await _da.DbLiveInstalledAsync().ConfigureAwait(false))
		{
			int appliedVersion = await _da.GetDbLiveVersionAsync().ConfigureAwait(false);
			migrationsToApply = migrationsToApply.Where(m => m.Version > appliedVersion);
		}

		foreach (InternalMigration migration in migrationsToApply)
		{
			await _da.ExecuteNonQueryAsync(migration.FileData.Content).ConfigureAwait(false);
			await _da.SetDbLiveVersionAsync(migration.Version, _timeProvider.UtcNow()).ConfigureAwait(false);
		}

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Self deploy completed.");
		}
	}
}
