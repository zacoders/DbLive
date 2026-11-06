

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

		await DeployMigrationBreakingAsync(parameters.CheckBreakingSecondRun).ConfigureAwait(false);
	}

	/// <summary>
	/// The breaking phase runs only after the normal deployment of this build completed: every code file has an applied row with its
	/// current hash. Otherwise a breaking run started too early (the Default run failed, Breaking before Default, a stale Breaking job
	/// after a rollback) drops what the running application still uses. A failure, not a skip: a skipped phase leaves the pipeline green.
	/// </summary>
	/// <exception cref="CodeNotDeployedException"/>
	private async Task ValidateCodeDeployedAsync()
	{
		List<string> notDeployed = [];

		foreach (CodeGroup group in await _project.GetCodeGroupsAsync().ConfigureAwait(false))
		{
			foreach (CodeItem codeItem in group.CodeItems)
			{
				CodeItemDto? row = await _da.FindCodeItemAsync(codeItem.FileData.RelativePath).ConfigureAwait(false);

				// the check of CodeItemDeployer: the guard passes exactly when the code deployment has nothing left to execute.
				if (row is null || row.Status != CodeItemStatus.Applied || !codeItem.FileData.MatchesHash(row.ContentHash))
				{
					notDeployed.Add(codeItem.FileData.RelativePath);
				}
			}
		}

		if (notDeployed.Count > 0)
		{
			throw new CodeNotDeployedException(
				notDeployed.Count,
				[.. notDeployed.Order(StringComparer.OrdinalIgnoreCase).Take(NamedNotDeployedFiles)]
			);
		}
	}

	private async Task DeployMigrationBreakingAsync(bool checkSecondRun)
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

			if (checkSecondRun)
			{
				await DeploySecondRunAsync(version, item, settings).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// The deployment tests run a migration breaking script twice: a breaking script that failed with TransactionWrapLevel None
	/// runs again on the next deployment, over what its first run already changed. The second run must not change the schema.
	/// </summary>
	/// <exception cref="SchemaChangedException"/>
	private async Task DeploySecondRunAsync(long version, MigrationItem item, MigrationItemSettings settings)
	{
		SchemaSnapshot afterFirstRun = await _da.GetSchemaSnapshotAsync().ConfigureAwait(false);

		await _migrationItemDeployer.DeployAsync(version, item, settings).ConfigureAwait(false);

		SchemaDiff diff = SchemaDiff.Compare(afterFirstRun, await _da.GetSchemaSnapshotAsync().ConfigureAwait(false));

		if (!diff.IsEmpty)
		{
			throw new SchemaChangedException(
				SchemaCheck.BreakingSecondRun,
				$"Deployment test: the breaking script {item.FileData.RelativePath} is not idempotent, its second run changed the schema:",
				diff
			);
		}
	}
}
