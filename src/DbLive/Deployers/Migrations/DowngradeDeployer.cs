
namespace DbLive.Deployers.Migrations;

public class DowngradeDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer
	) : IDowngradeDeployer
{
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
				.Where(m => m.Version > projectVersion)
				.OrderByDescending(m => m.Version)
				.ToList();

		
		// verify we have undo scripts for all migrations to be undone

		HashSet<int> undoVersions = undoMigrations.Select(u => u.Version).ToHashSet();
		HashSet<int> requiredVersions = Enumerable.Range(projectVersion + 1, projectVersion - databaseVersion).ToHashSet();

		List<int> missingUndoVersions = requiredVersions.Except(undoVersions).ToList();

		if (missingUndoVersions.Count != 0)
		{
			_logger.Error(
				"Missing undo scripts for migrations: {missingUndoScripts}",
				string.Join(", ", missingUndoVersions)
			);
			throw new DowngradeImpossibleException("Cannot perform downgrade due to missing undo scripts.");
		}

		// deploy undo migrations
		foreach (MigrationItemDto undoDto in undoMigrations)
		{
			_logger.Information("Undoing migration version {version}", undoDto.Version);
			
			string? undoContent = _da.GetMigrationContent(undoDto.Version, MigrationItemType.Undo);

			if (undoContent is null)
			{
				throw new DowngradeImpossibleException($"Undo content for migration version {undoDto.Version} is missing.");
			}

			MigrationItem undoItem = new()
			{
				MigrationItemType = MigrationItemType.Undo,
				FileData = new FileData{ Content = undoContent, FilePath = "", RelativePath = ""},
				Name = undoDto.Name				
			};
			
			_migrationItemDeployer.Deploy(undoDto.Version, undoItem);

			_logger.Information("Successfully undone migration version {version}", undoDto.Version);
		}
	}
}
