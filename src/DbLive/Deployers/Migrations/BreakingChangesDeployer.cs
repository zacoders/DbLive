

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

		var latestAppliedBreakingVersion =
			(await _da.GetMigrationsAsync().ConfigureAwait(false))
				.Where(m => m.Status == MigrationItemStatus.Applied)
				.Where(m => m.ItemType == MigrationItemType.Breaking)
				.Select(m => m.Version)
				.DefaultIfEmpty(0)
				.Max();

		IEnumerable<Migration> newMigrations = (await _project.GetMigrationsAsync().ConfigureAwait(false)).Where(m => m.Version > latestAppliedBreakingVersion);

		List<(int Version, MigrationItem Item)> breakingToApply = [];
		foreach (Migration? migration in newMigrations)
		{
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

		foreach ((int Version, MigrationItem Item) breaking in breakingToApply)
		{
			await _migrationItemDeployer.DeployAsync(breaking.Version, breaking.Item).ConfigureAwait(false);
		}
	}
}
