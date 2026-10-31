namespace DbLive.Deployers.Migrations;

public class MigrationItemDeployer(
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

			// the deployment transaction is already open, the item runs inside it.
			transactionWrapLevel = TransactionWrapLevel.Deployment;
		}

		await _transactionRunner.ExecuteWithinTransactionAsync(
			transactionWrapLevel == TransactionWrapLevel.Migration,
			isolationLevel,
			timeout,
			() => DeployInternalAsync(migrationVersion, migrationItem, transactionWrapLevel, isolationLevel, timeout)
		).ConfigureAwait(false);
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

		try
		{
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
				ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds,
				ErrorMessage = null
			};
			await _da.UpdateMigrationStateAsync(dto).ConfigureAwait(false);

			if (migrationItem.MigrationItemType == MigrationItemType.Undo)
			{
				await UpdateDateForRevertedMigrationsAsync(migrationVersion).ConfigureAwait(false);
			}

			if (migrationItem.MigrationItemType == MigrationItemType.Migration)
			{
				if (await _da.MigrationItemExistsAsync(migrationVersion, MigrationItemType.Undo).ConfigureAwait(false))
				{
					MigrationItemStateDto breakingDto = new()
					{
						Version = migrationVersion,
						ItemType = MigrationItemType.Undo,
						Status = MigrationItemStatus.None,
						AppliedUtc = null,
						ExecutionTimeMs = null,
						ErrorMessage = null
					};
					await _da.UpdateMigrationStateAsync(breakingDto).ConfigureAwait(false);
				}
			}
		}
		catch (DbLiveSqlException ex)
		{
			_logger.Error(
				ex,
				"Error deploying {RelativePath}. Type {MigrationType}. SQL Error: {SqlError}",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType,
				ex.SqlError
			);
			throw new MigrationDeploymentException($"""
				Error deploying {migrationItem.FileData.RelativePath} file.
				{ex.SqlError}.
				"""
			);
		}
		catch (Exception ex)
		{
			throw new MigrationDeploymentException($"Unexpected error deploying {migrationItem.FileData.RelativePath}. Type {migrationItem.MigrationItemType}.", ex);
		}
	}

	private async Task UpdateDateForRevertedMigrationsAsync(long migrationVersion)
	{
		MigrationItemStateDto migrationDto = new()
		{
			Version = migrationVersion,
			ItemType = MigrationItemType.Migration,
			Status = MigrationItemStatus.Reverted,
			AppliedUtc = null,
			ExecutionTimeMs = null,
			ErrorMessage = null
		};
		await _da.UpdateMigrationStateAsync(migrationDto).ConfigureAwait(false);

		if (await _da.MigrationItemExistsAsync(migrationVersion, MigrationItemType.Breaking).ConfigureAwait(false))
		{
			MigrationItemStateDto breakingDto = new()
			{
				Version = migrationVersion,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.Reverted,
				AppliedUtc = null,
				ExecutionTimeMs = null,
				ErrorMessage = null
			};
			await _da.UpdateMigrationStateAsync(breakingDto).ConfigureAwait(false);
		}
	}
}
