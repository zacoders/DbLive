
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

	public async Task DeployAsync(DeployParameters parameters)
	{
		IReadOnlyCollection<MigrationItemDto> allMigrations = await _da.GetMigrationsAsync().ConfigureAwait(false);

		HashSet<long> appliedInDb = allMigrations
			.Where(m => m.ItemType == MigrationItemType.Migration && m.Status == MigrationItemStatus.Applied)
			.Select(m => m.Version)
			.ToHashSet();

		HashSet<long> projectVersions = (await _project.GetMigrationsAsync().ConfigureAwait(false))
			.Select(m => m.Version)
			.ToHashSet();

		List<long> versionsToUndo = appliedInDb.Except(projectVersions).OrderByDescending(v => v).ToList();

		if (versionsToUndo.Count == 0)
		{
			return;
		}

		if (parameters.AllowDatabaseDowngrade == false)
		{
			_logger.Error(
				"Database downgrade detected. This operation is not allowed. Applied versions not in project: {versionsToUndo}. If this downgrade is intentional, enable it by setting {deployParametersClass}.{parameterName} to true.",
				string.Join(", ", versionsToUndo), nameof(DeployParameters), nameof(parameters.AllowDatabaseDowngrade)
			);
			throw new DowngradeNotAllowedException($"Database downgrade detected. This operation is not allowed. Applied versions not in project: {string.Join(", ", versionsToUndo)}. If this downgrade is intentional, enable it by setting {nameof(DeployParameters)}.{nameof(parameters.AllowDatabaseDowngrade)} to true.");
		}

		_logger.Information(
			"Starting database downgrade. Undo versions: {versionsToUndo}",
			string.Join(", ", versionsToUndo)
		);

		List<MigrationItemDto> undoMigrations =
			allMigrations
				.Where(m => m.ItemType == MigrationItemType.Undo)
				.Where(m => versionsToUndo.Contains(m.Version))
				.OrderByDescending(m => m.Version)
				.ToList();

		List<long> missingUndoVersions = versionsToUndo.Except(undoMigrations.Select(u => u.Version)).ToList();

		if (missingUndoVersions.Count != 0)
		{
			_logger.Error(
				"Missing undo scripts for migrations: {missingUndoScripts}",
				string.Join(", ", missingUndoVersions)
			);
			throw new DowngradeImpossibleException("Cannot perform downgrade due to missing undo scripts.");
		}

		Dictionary<long, string> undoContents = [];

		foreach (MigrationItemDto undoDto in undoMigrations)
		{
			string? undoContent = await _da.GetMigrationContentAsync(undoDto.Version, MigrationItemType.Undo).ConfigureAwait(false);

			if (undoContent is null)
			{
				throw new DowngradeImpossibleException($"Undo content for migration version {undoDto.Version} is missing.");
			}
			undoContents[undoDto.Version] = undoContent;
		}

		_logger.Information(
			"Downgrading database. Undo versions: {versions}",
			string.Join(", ", undoMigrations.Select(m => m.Version))
		);

		DbLiveSettings projectSettings = await projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

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

					await _migrationItemDeployer.DeployAsync(undoDto.Version, undoItem).ConfigureAwait(false);

					_logger.Information("Successfully undone migration version {version}", undoDto.Version);
				}
			}
		).ConfigureAwait(false);
	}
}
