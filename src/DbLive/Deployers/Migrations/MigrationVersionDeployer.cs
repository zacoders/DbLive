namespace DbLive.Deployers.Migrations;

public class MigrationVersionDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		IMigrationItemDeployer _migrationItemDeployer,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor projectSettingsAccessor
	) : IMigrationVersionDeployer
{

	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationVersionDeployer));

	public async Task DeployAsync(Migration migration, DeployParameters parameters)
	{
		if (migration.Items.Count == 0)
		{
			throw new InvalidOperationException($"Migration v{migration.Version} has no items to deploy.");
		}

		MigrationSettings migrationSettings = new();

		if (migration.Items.TryGetValue(MigrationItemType.Settings, out MigrationItem? settingsItem))
		{
			_logger.Information("Custom settings for migration v{version}", migration.Version);
			// todo: should we show a custom settings in the log? if yes, should we see global settings in the log?
			migrationSettings = SettingsTools.GetSettings<MigrationSettings>(settingsItem.FileData.Content);
		}

		DbLiveSettings _projectSettings = await projectSettingsAccessor.GetProjectSettingsAsync();

		migrationSettings = _projectSettings.GetMigrationSettings(migrationSettings);

		await _transactionRunner.ExecuteWithinTransactionAsync(
			migrationSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			migrationSettings.TransactionIsolationLevel!.Value,
			migrationSettings.MigrationTimeout!.Value,
			() => DeployInternalAsync(migration, parameters)
		);
	}

	internal async Task DeployInternalAsync(Migration migration, DeployParameters parameters)
	{
		migration.Items.TryGetValue(MigrationItemType.Migration, out MigrationItem? migrationItem);
		migration.Items.TryGetValue(MigrationItemType.Undo, out MigrationItem? undoItem);
		migration.Items.TryGetValue(MigrationItemType.Breaking, out MigrationItem? breakingItem);

		if (migrationItem is null)
		{
			throw new InternalException("Migration item must be specified.");
		}

		if (undoItem is not null)
		{
			if (parameters.UndoTestDeployment == UndoTestMode.MigrationUndoMigration)
			{
				await _migrationItemDeployer.DeployAsync(migration.Version, migrationItem);
				await _migrationItemDeployer.DeployAsync(migration.Version, undoItem);
			}

			if (parameters.UndoTestDeployment == UndoTestMode.MigrationBreakingUndoMigration)
			{
				await _migrationItemDeployer.DeployAsync(migration.Version, migrationItem);
				if (breakingItem is not null)
				{
					await _migrationItemDeployer.DeployAsync(migration.Version, breakingItem);
				}
				await _migrationItemDeployer.DeployAsync(migration.Version, undoItem);
			}

		}

		await _migrationItemDeployer.DeployAsync(migration.Version, migrationItem);

		DateTime migrationCompletedUtc = _timeProvider.UtcNow();

		await _da.SetCurrentMigrationVersionAsync(migration.Version, migrationCompletedUtc);
	}
}
