
namespace DbLive.Deployers.Migrations;

public class DowngradeDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IDowngradeDeployer
{

	private readonly DbLiveSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void Deploy(DeployParameters parameters)
	{
		// verify downgrade needed/allowed

		int databaseVersion = _da.GetCurrentMigrationVersion();

		int projectVersion = _project.GetMigrations().Max(m => m.Version);

		if (databaseVersion <= projectVersion)
		{
			return;
		}

		if (parameters.AllowDatabaseDowngrade == false)
		{
			_logger.Error(
			"Database downgrade detected but not allowed. Project Version: {projectVersion}, Database Version: {databaseVersion}. Please allow downgrade if intentional {DeployPrametersClass}.{ParameterName} ",
				projectVersion, databaseVersion, nameof(DeployParameters), nameof(parameters.AllowDatabaseDowngrade)
			);
			throw new DowngradeNotAllowedException("Database downgrade is not allowed.");
		}

		_logger.Information(
			"Starting database downgrade. Project Version: {projectVersion}, Database Version: {databaseVersion}",
			projectVersion, databaseVersion
		);

		// get undo migrations
		List<MigrationItemDto> undoMigrations = 
			_da.GetMigrations()
				.Where(m => m.ItemType == MigrationItemType.Undo)
				.Where(m => m.Version > projectVersion)
				.OrderByDescending(m => m.Version)
				.ToList();

		
		// verify we have undo scripts for all migrations to be undone

		HashSet<int> undoVersions = undoMigrations.Select(u => u.Version).ToHashSet();
		HashSet<int> requiredVersions = Enumerable.Range(projectVersion + 1, databaseVersion - projectVersion).ToHashSet();

		List<int> missingUndoVersions = requiredVersions.Except(undoVersions).ToList();

		if (missingUndoVersions.Count != 0)
		{
			_logger.Error(
				"Missing undo scripts for migrations: {missingUndoScripts}",
				string.Join(", ", missingUndoVersions)
			);
			throw new DowngradeImpossibleException("Cannot perform downgrade due to missing undo scripts.");
		}

		Dictionary<int, string> undoContents = [];

		// deploy undo migrations
		foreach (MigrationItemDto undoDto in undoMigrations)
		{
			_logger.Information("Undoing migration version {version}", undoDto.Version);

			string? undoContent = _da.GetMigrationContent(undoDto.Version, MigrationItemType.Undo);

			if (undoContent is null)
			{
				throw new DowngradeImpossibleException($"Undo content for migration version {undoDto.Version} is missing.");
			}
			undoContents[undoDto.Version] = undoContent;
		}

		_logger.Information(
			"Downgrading database from version {databaseVersion} to {projectVersion}. Undo versions: {versions}",
			databaseVersion,
			projectVersion,
			string.Join(", ", undoMigrations.Select(m => m.Version))
		);

		// all or nothing. online downgrades is not required.
		_transactionRunner.ExecuteWithinTransaction(
			true,
			_projectSettings.TransactionIsolationLevel,
			_projectSettings.DowngradeTimeout,
			() => {
				foreach (MigrationItemDto undoDto in undoMigrations)
				{
					MigrationItem undoItem = new()
					{
						MigrationItemType = MigrationItemType.Undo,
						FileData = new FileData { 
							Content = undoContents[undoDto.Version], 
							FilePath = "", 
							RelativePath = "" // todo: fill paths, required for logging.
						},
						Name = undoDto.Name
					};

					_migrationItemDeployer.Deploy(undoDto.Version, undoItem);

					_logger.Information("Successfully undone migration version {version}", undoDto.Version);
				}
			}
		);
	}
}
