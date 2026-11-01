namespace DbLive.Deployers.Migrations;

public class MigrationVersionDeployer(
		IMigrationItemDeployer _migrationItemDeployer,
		IDbLiveDA _da
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
		if (undoItem is not null && parameters.UndoTestDeployment == UndoTestMode.UndoCycle)
		{
			SchemaSnapshot beforeMigration = await _da.GetSchemaSnapshotAsync().ConfigureAwait(false);

			// a rollback before the breaking phase: the release is deployed, its breaking script is not.
			await DeployItemAsync(migration, migrationItem).ConfigureAwait(false);
			await DeployItemAsync(migration, undoItem).ConfigureAwait(false);
			await EnsureSchemaNotChangedAsync(
				beforeMigration,
				SchemaCheck.Undo,
				$"Undo test: {undoItem.FileData.RelativePath} does not restore the schema. The schema after the undo differs from the schema before {migrationItem.FileData.RelativePath}:"
			).ConfigureAwait(false);

			if (breakingItem is not null)
			{
				// a rollback after the breaking phase.
				await DeployItemAsync(migration, migrationItem).ConfigureAwait(false);

				// twice: a failed breaking script with TransactionWrapLevel None runs again on the next deployment.
				// the second run must not change the schema, so the undo below is tested against the state after one run, as in production.
				await DeployItemAsync(migration, breakingItem).ConfigureAwait(false);
				SchemaSnapshot afterFirstRun = await _da.GetSchemaSnapshotAsync().ConfigureAwait(false);
				await DeployItemAsync(migration, breakingItem).ConfigureAwait(false);
				await EnsureSchemaNotChangedAsync(
					afterFirstRun,
					SchemaCheck.BreakingSecondRun,
					$"Undo test: the breaking script {breakingItem.FileData.RelativePath} is not idempotent, its second run changed the schema:"
				).ConfigureAwait(false);

				await DeployItemAsync(migration, undoItem).ConfigureAwait(false);
				await EnsureSchemaNotChangedAsync(
					beforeMigration,
					SchemaCheck.Undo,
					$"Undo test: {undoItem.FileData.RelativePath} does not restore the schema after the breaking script {breakingItem.FileData.RelativePath}. The schema after the undo differs from the schema before {migrationItem.FileData.RelativePath}:"
				).ConfigureAwait(false);
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

	private async Task EnsureSchemaNotChangedAsync(SchemaSnapshot expected, SchemaCheck check, string message)
	{
		SchemaSnapshot actual = await _da.GetSchemaSnapshotAsync().ConfigureAwait(false);

		SchemaDiff diff = SchemaDiff.Compare(expected, actual);

		if (!diff.IsEmpty)
		{
			throw new SchemaChangedException(check, message, diff);
		}
	}
}
