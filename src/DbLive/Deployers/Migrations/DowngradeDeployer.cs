
namespace DbLive.Deployers.Migrations;

public class DowngradeDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor,
		ITimeProvider _timeProvider
	) : IDowngradeDeployer
{

	public async Task DeployAsync(DeployParameters parameters)
	{
		// verify downgrade needed/allowed

		int databaseVersion = await _da.GetCurrentMigrationVersionAsync();

		int projectVersion = (await _project.GetMigrationsAsync()).Max(m => m.Version);

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

		IReadOnlyCollection<MigrationItemDto> allMigrations = await _da.GetMigrationsAsync();

		// get undo migrations
		List<MigrationItemDto> undoMigrations =
				allMigrations
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
			string? undoContent = await _da.GetMigrationContentAsync(undoDto.Version, MigrationItemType.Undo);

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

		DbLiveSettings projectSettings = await projectSettingsAccessor.GetProjectSettingsAsync();

		// all or nothing. online downgrades is not required.
		await _transactionRunner.ExecuteWithinTransactionAsync(
			true,
			projectSettings.TransactionIsolationLevel,
			projectSettings.DowngradeTimeout,
			async () =>
			{
				foreach (MigrationItemDto undoDto in undoMigrations)
				{
					MigrationItem undoItem = new()
					{
						MigrationItemType = MigrationItemType.Undo,
						FileData = new FileData
						{
							Content = undoContents[undoDto.Version],
							FilePath = "",
							RelativePath = undoDto.RelativePath
						},
						Name = undoDto.Name
					};

					await _migrationItemDeployer.DeployAsync(undoDto.Version, undoItem);

					_logger.Information("Successfully undone migration version {version}", undoDto.Version);
				}

				DateTime migrationCompletedUtc = _timeProvider.UtcNow();
				await _da.SetCurrentMigrationVersionAsync(projectVersion, migrationCompletedUtc);
			}
		);
	}
}
