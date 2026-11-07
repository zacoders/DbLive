namespace DbLive.SelfDeployer;

internal class DbLiveSelfDeployer(
		ILogger _logger,
		ISettingsAccessor _projectSettings,
		IInternalDbLiveProject _internalProject,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner
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

		IReadOnlyList<InternalMigration> migrations = await _internalProject.GetMigrationsAsync().ConfigureAwait(false);

		await _transactionRunner.ExecuteWithinTransactionAsync(
			true,
			TranIsolationLevel.Serializable,
			TimeSpan.FromMinutes(5),
			async () =>
			{
				long appliedSelfMigrationVersion = 0;
				Version? minDbLiveLibVersion = null;

				if (await _da.DbLiveInstalledAsync().ConfigureAwait(false))
				{
					// before any change: a newer DbLive may have changed the dblive tables in a way this one cannot read.
					minDbLiveLibVersion = await _da.GetMinDbLiveLibVersionAsync().ConfigureAwait(false);

					DbLiveCompatibility.Check(minDbLiveLibVersion);

					appliedSelfMigrationVersion = await _da.GetSelfMigrationVersionAsync().ConfigureAwait(false);

					LogNewerSelfMigrations(migrations, appliedSelfMigrationVersion, minDbLiveLibVersion);
				}

				foreach (InternalMigration migration in migrations.Where(m => m.Version > appliedSelfMigrationVersion))
				{
					await _da.ExecuteNonQueryAsync(migration.FileData.Content).ConfigureAwait(false);
					await _da.SetSelfMigrationVersionAsync(migration.Version, _timeProvider.UtcNow()).ConfigureAwait(false);
				}

				// at the start of the deployment, not after it: the self-migrations cannot be undone, and a deployment
				// that fails later may already have written rows in the format of this DbLive.
				// After the check the database has this version or an older one, so it is never lowered.
				if (minDbLiveLibVersion is null || minDbLiveLibVersion < DbLiveCompatibility.MinCompatibleVersion)
				{
					await _da.SetMinDbLiveLibVersionAsync(DbLiveCompatibility.MinCompatibleVersion).ConfigureAwait(false);
				}
			}).ConfigureAwait(false);

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Self deploy completed.");
		}
	}

	// not an error: a newer DbLive deployed the database, and its requirement allows this DbLive (a rollback of the application,
	// or an older build deployed again). Its self-migrations stay; this DbLive applies none of its own.
	private void LogNewerSelfMigrations(IReadOnlyList<InternalMigration> migrations, long appliedVersion, Version? minDbLiveLibVersion)
	{
		long lastVersion = migrations.Select(m => m.Version).DefaultIfEmpty().Max();

		if (appliedVersion > lastVersion)
		{
			_logger.Information(
				"The dblive schema of the database is at self-migration {AppliedVersion}, newer than the last one of this DbLive ({LastVersion}): "
				+ "a newer DbLive deployed the database. Its minimum DbLive version ({MinDbLiveLibVersion}) allows this DbLive, so the deployment goes on.",
				appliedVersion,
				lastVersion,
				minDbLiveLibVersion
			);
		}
	}
}
