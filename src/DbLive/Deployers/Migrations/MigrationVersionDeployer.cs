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
	private readonly DbLiveSettings _projectSettings = projectSettingsAccessor.ProjectSettings;

	public void Deploy(Migration migration, DeployParameters parameters)
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

		migrationSettings = _projectSettings.GetMigrationSettings(migrationSettings);

		_transactionRunner.ExecuteWithinTransaction(
			migrationSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			migrationSettings.TransactionIsolationLevel!.Value,
			migrationSettings.MigrationTimeout!.Value,
			() => DeployInternal(migration, parameters)
		);
	}

	internal void DeployInternal(Migration migration, DeployParameters parameters)
	{
		migration.Items.TryGetValue(MigrationItemType.Migration, out var migrationItem);
		migration.Items.TryGetValue(MigrationItemType.Undo, out var undoItem);
		migration.Items.TryGetValue(MigrationItemType.Breaking, out var breakingItem);

		if (migrationItem is null)
		{
			throw new InternalException("Migration item must be specified.");
		}

		if (undoItem is not null)
		{
			_da.SaveMigrationItemState(new MigrationItemDto
			{
				Version = migration.Version,
				ItemType = MigrationItemType.Undo,
				Name = undoItem.Name,
				Status = MigrationItemStatus.None,
				Content = undoItem.FileData.Content,
				ContentHash = undoItem.FileData.ContentHash,
				CreatedUtc = _timeProvider.UtcNow(),
				AppliedUtc = null,
				ExecutionTimeMs = null
			});

			if (parameters.UndoTestDeployment == UndoTestMode.MigrationUndoMigration)
			{
				_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);
				_migrationItemDeployer.DeployMigrationItem(migration.Version, undoItem);
			}

			if (parameters.UndoTestDeployment == UndoTestMode.MigrationBreakingUndoMigration)
			{
				_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);
				if (breakingItem is not null)
				{
					_migrationItemDeployer.DeployMigrationItem(migration.Version, breakingItem);
				}
				_migrationItemDeployer.DeployMigrationItem(migration.Version, undoItem);
			}

		}

		_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);

		DateTime migrationCompletedUtc = _timeProvider.UtcNow();

		_da.SaveCurrentMigrationVersion(migration.Version, migrationCompletedUtc);
	}
}
