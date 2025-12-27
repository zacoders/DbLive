namespace DbLive.Deployers.Folder;

public class FolderDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IFolderDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(FolderDeployer));
	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	public void DeployFolder(ProjectFolder projectFolder, DeployParameters parameters)
	{
		_logger.Information("Deploying folder {ProjectFolder}.", projectFolder);

		ReadOnlyCollection<GenericItem> items = _project.GetFolderItems(projectFolder);

		if (items.Count == 0)
		{
			_logger.Information("Folder {ProjectFolder} is empty.", projectFolder);
			return;
		}

		foreach (GenericItem item in items)
		{
			DeployItem(projectFolder, item);
		}

		_logger.Information("Deployment of the folder {ProjectFolder} successfully completed..", projectFolder);
	}

	private void DeployItem(ProjectFolder projectFolder, GenericItem item)
	{
		_logger.Information("Deploying item: {filePath}", item.FileData.FileName);

		DateTime startedUtc = _timeProvider.UtcNow();

		_da.ExecuteNonQuery(
			item.FileData.Content,
			_projectSettings.TransactionIsolationLevel,
			projectFolder switch
			{
				ProjectFolder.BeforeDeploy => _projectSettings.BeforeDeployFolderTimeout,
				ProjectFolder.AfterDeploy => _projectSettings.AfterDeployFolderTimeout,
				_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
			}
		);

		DateTime completedUtc = _timeProvider.UtcNow();

		_da.MarkItemAsApplied(projectFolder, item.FileData.RelativePath, startedUtc, completedUtc, (long)(completedUtc - startedUtc).TotalMilliseconds);
	}
}
