namespace DbLive.Deployers.Migrations;

internal class MigrationChecksumValidator(
	ILogger logger,
	ISettingsAccessor settingsAccessor,
	IDbLiveProject project,
	IDbLiveDA da
) : IMigrationChecksumValidator
{
	private readonly ILogger _logger = logger.ForContext(typeof(MigrationChecksumValidator));

	public async Task ValidateAsync(DeployParameters parameters)
	{
		if (!await da.DbLiveInstalledAsync().ConfigureAwait(false))
		{
			return;
		}

		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		IReadOnlyCollection<MigrationItemDto> dbItems = await da.GetMigrationsAsync().ConfigureAwait(false);
		IReadOnlyList<Migration> projectMigrations = await project.GetMigrationsAsync().ConfigureAwait(false);

		Dictionary<(long Version, MigrationItemType ItemType), MigrationItem> projectItems =
			projectMigrations
				.SelectMany(m => m.Items.Select(i => ((m.Version, i.Key), i.Value)))
				.ToDictionary(x => x.Item1, x => x.Value);

		List<(MigrationItemDto DbItem, MigrationItem ProjectItem)> mismatches = [];

		foreach (MigrationItemDto dbItem in dbItems.Where(d => d.Status == MigrationItemStatus.Applied))
		{
			if (!projectItems.TryGetValue((dbItem.Version, dbItem.ItemType), out MigrationItem? projectItem))
			{
				continue;
			}

			if (dbItem.ContentHash == projectItem.FileData.ContentHash)
			{
				continue;
			}

			mismatches.Add((dbItem, projectItem));
		}

		if (mismatches.Count == 0)
		{
			return;
		}

		if (parameters.RepairMigrationChecksums)
		{
			foreach ((MigrationItemDto dbItem, MigrationItem projectItem) in mismatches)
			{
				await da.RepairMigrationChecksumAsync(new MigrationChecksumRepairDto
				{
					Version = dbItem.Version,
					ItemType = dbItem.ItemType,
					ContentHash = projectItem.FileData.ContentHash,
					Content = dbItem.ItemType == MigrationItemType.Undo ? projectItem.FileData.Content : null,
					RelativePath = projectItem.FileData.RelativePath
				}).ConfigureAwait(false);

				_logger.Information(
					"Repaired checksum for applied migration item '{ItemType}' version {Version} ({RelativePath}).",
					dbItem.ItemType,
					dbItem.Version,
					projectItem.FileData.RelativePath
				);
			}

			return;
		}

		if (projectSettings.MigrationChecksumMode == MigrationChecksumMode.Warn)
		{
			foreach ((MigrationItemDto dbItem, MigrationItem projectItem) in mismatches)
			{
				_logger.Warning(
					"Applied migration item '{ItemType}' version {Version} ({RelativePath}) has changed since it was applied.",
					dbItem.ItemType,
					dbItem.Version,
					projectItem.FileData.RelativePath
				);
			}

			return;
		}

		string mismatchDetails = string.Join(
			Environment.NewLine,
			mismatches.Select(m =>
				$"  - version {m.DbItem.Version}, {m.DbItem.ItemType}: {m.ProjectItem.FileData.RelativePath}")
		);

		throw new MigrationChecksumMismatchException(
			$"""
			Applied migration checksum mismatch detected. The following files have changed since they were applied:
			{mismatchDetails}
			Revert the file changes in source control, or redeploy with RepairMigrationChecksums = true after confirming the database schema already matches the project files.
			"""
		);
	}
}
