namespace EasyFlow.Deployers;

public class BreakingChangesDeployer(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider,
		MigrationItemDeployer _migrationItemDeployer
	)
{
	private readonly ILogger _logger = _logger.ForContext(typeof(BreakingChangesDeployer));

	public void DeployBreakingChanges(DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		_logger.Information("Deploying breaking changes.");

		DeployBreakingMigration(/*, parameters*/);
	}

	private void DeployBreakingMigration(/*, DeployParameters parameters*/)
	{
		var dbItems = _da.GetNonAppliedBreakingMigrationItems();

		if (dbItems.Count == 0) return;

		Dictionary<(int Version, string Name), MigrationItemDto> breakingToApply = dbItems.ToDictionary(i => (i.Version, i.Name));

		int minVersionOfMigration = breakingToApply.Min(b => b.Value.Version);

		var migrations = _project.GetMigrations().Where(m => m.Version >= minVersionOfMigration);

		foreach (var migration in migrations)
		{
			var key = (migration.Version, migration.Name);
			if (!breakingToApply.ContainsKey(key)) continue;

			var breakingChnagesItem = _project.GetMigrationItems(migration.FolderPath)
				.Where(mi => mi.MigrationItemType == MigrationItemType.BreakingChange)
				.Single();

			if (breakingChnagesItem.FileData.Crc32Hash != breakingToApply[key].ContentHash)
			{
				throw new FileContentChangedException(
					breakingChnagesItem.FileData.RelativePath,
					breakingChnagesItem.FileData.Crc32Hash,
					breakingToApply[key].ContentHash
				);
			}

			// TODO: transaction should be configurable?
			using var tran = TransactionScopeManager.Create();
			{
				_migrationItemDeployer.DeployMigrationItem(false, migration, breakingChnagesItem);
				_da.SaveMigration(migration.Version, migration.Name, _timeProvider.UtcNow());
				tran.Complete();
			}
		}
	}
}
