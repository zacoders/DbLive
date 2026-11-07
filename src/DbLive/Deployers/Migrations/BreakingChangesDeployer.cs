

namespace DbLive.Deployers.Migrations;

public class BreakingChangesDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ICodeBreakingDeployer _codeBreakingDeployer
	) : IBreakingChangesDeployer
{
	/// <summary>
	/// How many of the code files that are not deployed <see cref="CodeNotDeployedException"/> names.
	/// </summary>
	internal const int NamedNotDeployedFiles = 5;

	private readonly ILogger _logger = _logger.ForContext(typeof(BreakingChangesDeployer));

	public async Task DeployAsync(DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		_logger.Information("Deploying breaking changes.");

		await ValidateCodeDeployedAsync().ConfigureAwait(false);

		// dependents are dropped before what they depend on: on PostgreSQL a view of the old version blocks the 'drop column'
		// of a migration breaking script.
		await _codeBreakingDeployer.DeployAsync().ConfigureAwait(false);

		await DeployMigrationBreakingAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// The breaking phase runs only after the normal deployment of this build completed: every code file has an applied row with its
	/// current hash. Otherwise a breaking run started too early (the Default run failed, Breaking before Default) drops what the running
	/// application still uses. A failure, not a skip: a skipped phase leaves the pipeline green. Not a check that the release is still the
	/// current one: a stale Breaking job of a release that a later one replaced can pass (0096-41, rejected).
	/// </summary>
	/// <exception cref="CodeNotDeployedException"/>
	private async Task ValidateCodeDeployedAsync()
	{
		IReadOnlyList<CodeGroup> codeGroups = await _project.GetCodeGroupsAsync().ConfigureAwait(false);

		CodeRows rows = new(await _da.GetCodeItemsAsync().ConfigureAwait(false));

		// the check of CodeDeployer: the guard passes exactly when the code deployment has nothing left to execute.
		List<string> notDeployed =
		[
			.. codeGroups
				.SelectMany(group => group.CodeItems)
				.Where(codeItem => !rows.IsDeployed(codeItem.FileData))
				.Select(codeItem => codeItem.FileData.RelativePath)
		];

		if (notDeployed.Count > 0)
		{
			throw new CodeNotDeployedException(
				notDeployed.Count,
				[.. notDeployed.Order(StringComparer.OrdinalIgnoreCase).Take(NamedNotDeployedFiles)]
			);
		}
	}

	private async Task DeployMigrationBreakingAsync()
	{
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
			_logger.Information("No migration breaking scripts to apply.");
			return;
		}

		// each breaking script is deployed by its own settings, in its own transaction.
		foreach ((long version, MigrationItem item, MigrationItemSettings settings) in breakingToApply.OrderBy(b => b.Version))
		{
			await _migrationItemDeployer.DeployAsync(version, item, settings).ConfigureAwait(false);
		}
	}
}
