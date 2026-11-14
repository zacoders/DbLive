namespace DbLive.Deployers.Migrations;

internal class MigrationVersionDeployer(
		IMigrationItemDeployer _migrationItemDeployer
	) : IMigrationVersionDeployer
{
	public async Task DeployAsync(Migration migration)
	{
		if (migration.Items.Count == 0)
		{
			throw new InvalidOperationException($"Migration v{migration.Version} has no items to deploy.");
		}

		if (!migration.Items.TryGetValue(MigrationItemType.Migration, out MigrationItem? migrationItem))
		{
			throw new InternalException("Migration item must be specified.");
		}

		await _migrationItemDeployer.DeployAsync(
			migration.Version,
			migrationItem,
			migration.GetItemSettings(MigrationItemType.Migration)
		).ConfigureAwait(false);
	}
}
