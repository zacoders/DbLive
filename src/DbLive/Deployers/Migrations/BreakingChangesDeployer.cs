

namespace DbLive.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor _projectSettingsAccessor
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

		List<(long Version, MigrationItem Item)> breakingToApply = [];
		foreach (Migration migration in await _project.GetMigrationsAsync().ConfigureAwait(false))
		{
			if (!breakingVersionsToApply.Contains(migration.Version))
			{
				continue;
			}

			if (migration.Items.TryGetValue(MigrationItemType.Breaking, out MigrationItem? breakingItem))
			{
				breakingToApply.Add((migration.Version, breakingItem));
			}
		}

		if (breakingToApply.Count == 0)
		{
			_logger.Information("No breaking changes to apply.");
			return;
		}

		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		await _transactionRunner.ExecuteWithinTransactionAsync(
			projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			projectSettings.TransactionIsolationLevel,
			projectSettings.MigrationTimeout,
			async () =>
			{
				foreach ((long Version, MigrationItem Item) breaking in breakingToApply.OrderBy(b => b.Version))
				{
					await _migrationItemDeployer.DeployAsync(breaking.Version, breaking.Item).ConfigureAwait(false);
				}
			}
		).ConfigureAwait(false);
	}
}
