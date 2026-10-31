

namespace DbLive.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer
	) : IBreakingChangesDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(BreakingChangesDeployer));

	public async Task DeployAsync(DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		_logger.Information("Deploying breaking changes.");

		IReadOnlyCollection<MigrationItemDto> migrations = await _da.GetMigrationsAsync().ConfigureAwait(false);

		HashSet<long> notAppliedBreakingVersions =
			migrations
				.Where(m => m.Status != MigrationItemStatus.Applied)
				.Where(m => m.ItemType == MigrationItemType.Breaking)
				.Select(m => m.Version)
				.ToHashSet();

		HashSet<long> appliedMigrationVersions =
			migrations
				.Where(m => m.Status == MigrationItemStatus.Applied)
				.Where(m => m.ItemType == MigrationItemType.Migration)
				.Select(m => m.Version)
				.ToHashSet();

		HashSet<long> breakingVersionsToApply = appliedMigrationVersions.Intersect(notAppliedBreakingVersions).ToHashSet();

		List<(long Version, MigrationItem Item, MigrationItemSettings Settings)> breakingToApply = [];
		foreach (Migration migration in await _project.GetMigrationsAsync().ConfigureAwait(false))
		{
			if (!breakingVersionsToApply.Contains(migration.Version))
			{
				continue;
			}

			if (migration.Items.TryGetValue(MigrationItemType.Breaking, out MigrationItem? breakingItem))
			{
				breakingToApply.Add((migration.Version, breakingItem, migration.GetItemSettings(MigrationItemType.Breaking)));
			}
		}

		if (breakingToApply.Count == 0)
		{
			_logger.Information("No breaking changes to apply.");
			return;
		}

		// each breaking script is deployed by its own settings, in its own transaction.
		foreach ((long version, MigrationItem item, MigrationItemSettings settings) in breakingToApply.OrderBy(b => b.Version))
		{
			await _migrationItemDeployer.DeployAsync(version, item, settings).ConfigureAwait(false);
		}
	}
}
