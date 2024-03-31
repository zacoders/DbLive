using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider,
		IMigrationItemDeployer _migrationItemDeployer
	)
{
	private readonly ILogger _logger = logger.ForContext(typeof(BreakingChangesDeployer));

	public void DeployBreakingChanges(DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		_logger.Information("Deploying breaking changes.");

		var dbItems = _da.GetNonAppliedBreakingMigrationItems();

		if (dbItems.Count == 0) return;

		Dictionary<VersionNameKey, MigrationItemDto> breakingToApply = 
			dbItems.ToDictionary(i => new VersionNameKey(i.Version, i.Name));

		int minVersionOfMigration = breakingToApply.Min(b => b.Value.Version);

		var migrations = _project.GetMigrations().Where(m => m.Version >= minVersionOfMigration);

		foreach (Migration migration in migrations)
		{
			VersionNameKey key = new(migration.Version, migration.Name);
			if (!breakingToApply.ContainsKey(key)) continue;

			var breakingChnagesItem = migration.Items
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

	private record VersionNameKey(int Version, string Name);
}
