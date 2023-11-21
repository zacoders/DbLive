namespace EasyFlow.Deployers;

public class BreakingChangesDeployer(
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider,
		MigrationItemDeployer _migrationItemDeployer
	)
{
	private static readonly ILogger Logger = Log.ForContext(typeof(BreakingChangesDeployer));

	public void DeployBreakingChanges(string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		DeployBreakingMigration(sqlConnectionString, parameters);
	}

	private void DeployBreakingMigration(string sqlConnectionString, DeployParameters parameters)
	{
		var dbItems = _da.GetNonAppliedBreakingMigrationItems(sqlConnectionString);
		Dictionary<(int Version, string Name), MigrationItemDto> breakingToApply = dbItems.ToDictionary(i => (i.Version, i.Name));

		int minVersionOfMigration = breakingToApply.Min(b => b.Value.Version);

		var migrations = _project.GetMigrations().Where(m => m.Version >= minVersionOfMigration);

		foreach (var migration in migrations)
		{
			var key = (migration.Version, migration.Name);
			if (!breakingToApply.ContainsKey(key)) continue;

			var breakingChnagesItem = _project.GetMigrationItems(migration.FolderPath)
				.Where(mi => mi.MigrationType == MigrationItemType.BreakingChange)
				.Single();

			if (breakingChnagesItem.FileData.Crc32Hash != breakingToApply[key].ContentHash)
			{
				throw new FileContentChangedException(
					breakingChnagesItem.FileData.RelativePath,
					breakingChnagesItem.FileData.Crc32Hash,
					breakingToApply[key].ContentHash
				);
			}

			using var tran = TransactionScopeManager.Create();
			{
				_migrationItemDeployer.DeployMigrationItem(sqlConnectionString, false, migration, breakingChnagesItem, new[] { MigrationItemType.BreakingChange });
				_da.SaveMigration(sqlConnectionString, migration.Version, migration.Name, _timeProvider.UtcNow());
				tran.Complete();
			}
		}
	}
}
