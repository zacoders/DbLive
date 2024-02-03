using EasyFlow.Adapter;

namespace EasyFlow.Deployers;

public class FolderDeployer(
		ILogger logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider
	)
{
	private readonly ILogger _logger = logger.ForContext(typeof(FolderDeployer));

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
		_logger.Information("Deploying item: {filePath}", item.FileData.FilePath.GetLastSegment());

		DateTime startedUtc = _timeProvider.UtcNow();
		_da.ExecuteNonQuery(item.FileData.Content);
		DateTime completedUtc = _timeProvider.UtcNow();

		_da.MarkItemAsApplied(projectFolder, item.FileData.RelativePath, startedUtc, completedUtc, (int)(completedUtc - startedUtc).TotalMilliseconds);
	}
}
