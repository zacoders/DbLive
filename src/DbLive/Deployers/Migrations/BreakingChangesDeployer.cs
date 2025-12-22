using DbLive.Adapter;

namespace DbLive.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		IMigrationItemDeployer _migrationItemDeployer
	) : IBreakingChangesDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(BreakingChangesDeployer));

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

			MigrationItemDto breakingDto = breakingToApply[key];

			var breakingChnagesItem = migration.Items
				.Where(mi => mi.MigrationItemType == MigrationItemType.Breaking)
				.Single();

			if (breakingChnagesItem.FileData.Crc32Hash != breakingDto.ContentHash)
			{
				throw new FileContentChangedException(
					breakingChnagesItem.FileData.RelativePath,
					breakingChnagesItem.FileData.Crc32Hash,
					breakingDto.ContentHash
				);
			}

			// TODO: transaction should be configurable?
			using var tran = TransactionScopeManager.Create();
			{
				var stopwatch = _timeProvider.StartNewStopwatch();

				_migrationItemDeployer.DeployMigrationItem(false, migration, breakingChnagesItem);

				stopwatch.Stop();

				breakingDto.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
				breakingDto.AppliedUtc = _timeProvider.UtcNow();

				_da.SaveMigrationItemState(breakingDto);

				tran.Complete();
			}
		}
	}

	[ExcludeFromCodeCoverage]
	private record VersionNameKey(int Version, string Name);
}
