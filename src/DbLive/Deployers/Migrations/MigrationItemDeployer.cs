namespace DbLive.Deployers.Migrations;

internal class MigrationItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ITransactionRunner _transactionRunner,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	public async Task DeployAsync(long migrationVersion, MigrationItem migrationItem, MigrationItemSettings itemSettings)
	{
		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		TransactionWrapLevel transactionWrapLevel = itemSettings.TransactionWrapLevel ?? projectSettings.TransactionWrapLevel;
		TranIsolationLevel isolationLevel = itemSettings.TransactionIsolationLevel ?? projectSettings.TransactionIsolationLevel;
		TimeSpan timeout = itemSettings.Timeout ?? projectSettings.MigrationTimeout;

		TranIsolationLevelMapper.ValidateForProvider(isolationLevel, _da.Provider);

		if (projectSettings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
		{
			if (transactionWrapLevel == TransactionWrapLevel.None)
			{
				_logger.Warning(
					"{relativePath} has TransactionWrapLevel None, but the project runs the whole deployment in one transaction (TransactionWrapLevel Deployment). The script runs inside the deployment transaction.",
					migrationItem.FileData.RelativePath
				);
			}

			// the deployment transaction is already open, the item runs inside it, at its isolation level: the data access runs a script
			// in a transaction at the level of the transaction. The level of the item is only validated, above.
			transactionWrapLevel = TransactionWrapLevel.Deployment;
			isolationLevel = projectSettings.TransactionIsolationLevel;
		}

		// the transaction of one file: its timeout error goes into the error of the file, as the timeout of the script does.
		TransactionTimeout transactionTimeout = new(
			timeout,
			migrationItem.MigrationItemType == MigrationItemType.Breaking ? "The transaction of the breaking script" : "The transaction of the migration",
			itemSettings.Timeout is null ? nameof(DbLiveSettings.MigrationTimeout) : "Timeout of the item settings"
		);

		try
		{
			await _transactionRunner.ExecuteWithinTransactionAsync(
				transactionWrapLevel == TransactionWrapLevel.Migration,
				isolationLevel,
				transactionTimeout,
				() => DeployInternalAsync(migrationVersion, migrationItem, transactionWrapLevel, isolationLevel, timeout)
			).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// SqlError is empty for an error that does not come from the server, such as a timeout. An error that is not a
			// DbLiveSqlException (the write of the state of the item, the commit) gives its message.
			string error = ex.GetSqlErrorOrMessage();

			_logger.Error(
				ex,
				"Error deploying {RelativePath}. Type {MigrationType}. SQL Error: {SqlError}",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType,
				error
			);
			throw new MigrationDeploymentException($"""
				Error deploying {migrationItem.FileData.RelativePath} file.
				{error}
				""",
				ex
			);
		}
	}

	private async Task DeployInternalAsync(
		long migrationVersion,
		MigrationItem migrationItem,
		TransactionWrapLevel transactionWrapLevel,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	)
	{
		DateTime startTimeUtc = _timeProvider.UtcNow();

		_logger.Information(
			"Deploying {relativePath}. Type {migrationType}. Transaction {transactionWrapLevel}, isolation level {isolationLevel}, timeout {timeout}.",
			migrationItem.FileData.RelativePath,
			migrationItem.MigrationItemType,
			transactionWrapLevel,
			isolationLevel,
			timeout
		);

		await _da.ExecuteNonQueryAsync(
			migrationItem.FileData.Content,
			isolationLevel,
			timeout
		).ConfigureAwait(false);

		DateTime migrationEndTime = _timeProvider.UtcNow();
		MigrationItemStateDto dto = new()
		{
			Version = migrationVersion,
			ItemType = migrationItem.MigrationItemType,
			Status = MigrationItemStatus.Applied,
			AppliedUtc = migrationEndTime,
			ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds
		};
		await _da.UpdateMigrationStateAsync(dto).ConfigureAwait(false);
	}
}
