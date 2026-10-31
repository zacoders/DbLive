namespace DbLive.Deployers.Migrations;

public class MigrationVersionDeployer(
		IMigrationItemDeployer _migrationItemDeployer
	) : IMigrationVersionDeployer
{
	public async Task DeployAsync(Migration migration, DeployParameters parameters)
	{
		if (migration.Items.Count == 0)
		{
			throw new InvalidOperationException($"Migration v{migration.Version} has no items to deploy.");
		}

		_ = migration.Items.TryGetValue(MigrationItemType.Migration, out MigrationItem? migrationItem);
		_ = migration.Items.TryGetValue(MigrationItemType.Undo, out MigrationItem? undoItem);
		_ = migration.Items.TryGetValue(MigrationItemType.Breaking, out MigrationItem? breakingItem);

		if (migrationItem is null)
		{
			throw new InternalException("Migration item must be specified.");
		}

		// each item is deployed by its own settings, in its own transaction.
		if (undoItem is not null)
		{
			if (parameters.UndoTestDeployment == UndoTestMode.MigrationUndoMigration)
			{
				await DeployItemAsync(migration, migrationItem).ConfigureAwait(false);
				await DeployItemAsync(migration, undoItem).ConfigureAwait(false);
			}

			if (parameters.UndoTestDeployment == UndoTestMode.MigrationBreakingUndoMigration)
			{
				await DeployItemAsync(migration, migrationItem).ConfigureAwait(false);
				if (breakingItem is not null)
				{
					await DeployItemAsync(migration, breakingItem).ConfigureAwait(false);
				}
				await DeployItemAsync(migration, undoItem).ConfigureAwait(false);
			}

		}

		await DeployItemAsync(migration, migrationItem).ConfigureAwait(false);
	}

	private Task DeployItemAsync(Migration migration, MigrationItem item)
		=> _migrationItemDeployer.DeployAsync(
			migration.Version,
			item,
			migration.GetItemSettings(item.MigrationItemType)
		);
}
