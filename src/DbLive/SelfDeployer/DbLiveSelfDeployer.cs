
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

	public void Deploy()
	{
		DbLiveSettings projectSettings = _projectSettings.ProjectSettings;

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Starting self deploy.");
		}

		IEnumerable<InternalMigration> migrationsToApply = _internalProject.GetMigrations();

		if (_da.DbLiveInstalled())
		{
			int appliedVersion = _da.GetDbLiveVersion();
			migrationsToApply = migrationsToApply.Where(m => m.Version > appliedVersion);
		}

		foreach (var migration in migrationsToApply)
		{
			_da.ExecuteNonQuery(migration.FileData.Content);
			_da.SetDbLiveVersion(migration.Version, _timeProvider.UtcNow());
		}

		if (projectSettings.LogSelfDeploy)
		{
			_logger.Information("Self deploy completed.");
		}
	}
}
