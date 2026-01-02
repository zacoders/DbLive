
namespace DbLive.Deployers.Migrations;

public class MigrationItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IMigrationItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(MigrationItemDeployer));

	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	public void Deploy(int migrationVersion, MigrationItem migrationItem)
	{
		DateTime startTimeUtc = _timeProvider.UtcNow();
		MigrationItemStatus? migrationStatus = null;
		string? errorMessage = null;
		try
		{
			_logger.Information(
				"Deploying {relativePath}. Type {migrationType}.",
				migrationItem.FileData.RelativePath,
				migrationItem.MigrationItemType
			);

			_da.ExecuteNonQuery(
				migrationItem.FileData.Content,
				_projectSettings.TransactionIsolationLevel,
				_projectSettings.MigrationTimeout
			);

			migrationStatus = MigrationItemStatus.Applied;
		}
		catch (Exception ex)
		{
			migrationStatus = MigrationItemStatus.Failed;
			errorMessage = ex.ToString();
			throw new MigrationDeploymentException($"Migration file deployment error. File path: {migrationItem.FileData.RelativePath}", ex);
		}
		finally
		{
			DateTime migrationEndTime = _timeProvider.UtcNow();
			MigrationItemStateDto dto = new()
			{
				Version = migrationVersion,
				ItemType = migrationItem.MigrationItemType,
				Status = migrationStatus!.Value,				
				AppliedUtc = migrationEndTime,
				ExecutionTimeMs = (long)(migrationEndTime - startTimeUtc).TotalMilliseconds,
				ErrorMessage = errorMessage
			};
			_da.UpdateMigrationState(dto);
		}
	}
}
