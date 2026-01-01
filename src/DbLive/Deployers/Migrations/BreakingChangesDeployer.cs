namespace DbLive.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger _logger,
		IDbLiveProjectBase _project,
		IDbLiveDA _da,
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

		var latestAppliedBreakingVersion =
			_da.GetMigrations()
				.Where(m => m.Status == MigrationItemStatus.Applied)
				.Where(m => m.ItemType == MigrationItemType.Breaking)
				.Select(m => m.Version)
				.DefaultIfEmpty(0)
				.Max();

		var newMigrations = _project.GetMigrations().Where(m => m.Version > latestAppliedBreakingVersion);

		List<(int Version, MigrationItem Item)> breakingToApply = [];
		foreach (var migration in newMigrations)
		{
			if (migration.Items.TryGetValue(MigrationItemType.Breaking, out var breakingItem))
			{
				breakingToApply.Add((migration.Version, breakingItem));
			}
		}

		if (breakingToApply.Count == 0)
		{
			_logger.Information("No breaking changes to apply.");
			return;
		}

		foreach (var breaking in breakingToApply)
		{
			_migrationItemDeployer.DeployMigrationItem(false, breaking.Version, breaking.Item);
		}
	}
}
