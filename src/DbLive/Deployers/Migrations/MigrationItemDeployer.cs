namespace DbLive.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	public async Task DeployAsync(int migrationVersion, MigrationItem migrationItem)
	{
		DateTime startTimeUtc = _timeProvider.UtcNow();

		try
		{
			_logger.Information(
				"Deploying {relativePath}. Type {migrationType}.",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType
			);

			DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

			await _da.ExecuteNonQueryAsync(
				migrationItem.FileData.Content,
				projectSettings.TransactionIsolationLevel,
				projectSettings.MigrationTimeout
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
				ex.SqlError,
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType
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

	private async Task UpdateDateForRevertedMigrationsAsync(int migrationVersion)
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
