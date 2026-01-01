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

	public void DeployMigration(Migration migration, DeployParameters parameters)
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

		if (migrationItem is null)
		{
			throw new InternalException("Migration item must be specified.");
		}

		if (parameters.UndoTestDeployment && undoItem is not null)
		{
			_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);
			_migrationItemDeployer.DeployMigrationItem(migration.Version, undoItem);
			_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);
		}
		else
		{
			_migrationItemDeployer.DeployMigrationItem(migration.Version, migrationItem);
		}

		DateTime migrationCompletedUtc = _timeProvider.UtcNow();

		_da.SaveCurrentMigrationVersion(migration.Version, migrationCompletedUtc);
	}
}
