
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
			(await _da.GetMigrationsAsync())
				.Where(m => m.Status == MigrationItemStatus.Applied)
				.Where(m => m.ItemType == MigrationItemType.Breaking)
				.Select(m => m.Version)
				.DefaultIfEmpty(0)
				.Max();

		var newMigrations = (await _project.GetMigrationsAsync()).Where(m => m.Version > latestAppliedBreakingVersion);

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
			await _migrationItemDeployer.DeployAsync(breaking.Version, breaking.Item);
		}
	}
}
